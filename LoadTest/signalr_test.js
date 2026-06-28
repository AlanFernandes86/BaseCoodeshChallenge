import ws from 'k6/ws';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

// Métrica customizada que aparecerá no relatório final
const orderLatency = new Trend('order_roundtrip_time', true); // 'true' significa que é métrica de tempo (ms)

export const options = {
  // Para alcançar um grande volume (milhões), usamos mais VUs
  // 100 usuários virtuais simulando conexões. Ajuste conforme sua máquina aguentar.
  vus: 500,
  duration: '3m15s', // 1 minuto de envios + 10s de 'cool down' (espera)
};

// URL do backend SignalR - Ajuste a porta se necessário
const BASE_URL = 'ws://localhost:5000';
const HUB_PATH = '/orderhub'; // Rota encontrada no backend

export default function () {
  const wsUrl = `${BASE_URL}${HUB_PATH}`;

  const res = ws.connect(wsUrl, function (socket) {
    socket.on('open', () => {
      // 1. Handshake do SignalR
      socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\x1e');
    });

    // Fila para guardar os tempos exatos em que cada ordem foi disparada
    const sendTimes = [];

    socket.on('message', (msg) => {
      // Verifica se é a confirmação do Handshake
      if (msg === '{}\x1e') {

        // 3. Monta a mensagem de Invocação
        // Com base no backend: SubmitOrder(string symbol, string side, decimal quantity, decimal price)
        // Validadores: PETR4/VALE3/VIIA4, Buy/Sell, qty inteiro, preço < 1000 e mult 0.01
        const invocationMsg = {
          type: 1,
          target: 'SubmitOrder',
          arguments: ['PETR4', 'Buy', 1, 0.01]
        };

        const payload = JSON.stringify(invocationMsg) + '\x1e';

        // 4. Inicia o envio frenético
        const intervalId = socket.setInterval(function () {
          // Guarda o timestamp exato do envio
          sendTimes.push(Date.now());
          socket.send(payload);
        }, 10);

        // 5. Para de enviar após 60s, mas não fecha a conexão ainda
        socket.setTimeout(function () {
          socket.clearInterval(intervalId);
        }, 180000);
      }
      else if (msg.includes('OrderResult')) {
        // Se for a resposta de uma ordem, calculamos o tempo de ida e volta
        const sentAt = sendTimes.shift(); // Tira o timestamp mais antigo da fila
        if (sentAt) {
          orderLatency.add(Date.now() - sentAt);
        }
      }

      // Removi os console.log para não travar o terminal do k6 com milhões de logs
    });

    socket.on('close', () => { });
    socket.on('error', (e) => {
      if (e.error() != "websocket: close sent") {
        console.log('An unexpected error occurred: ', e.error());
      }
    });

    // O socket fica aberto esperando respostas até os 65 segundos e então fecha graciosamente
    socket.setTimeout(function () {
      socket.close();
    }, 190000);
  });

  check(res, { 'Conexão estabelecida': (r) => r && r.status === 101 });
  sleep(1);
}
