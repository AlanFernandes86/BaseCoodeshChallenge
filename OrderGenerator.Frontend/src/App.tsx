import React, { useEffect, useState } from 'react'
import { signalRService } from './services/signalRService'
import type { OrderResult } from './services/signalRService'
import { OrderForm, SYMBOL_META } from './components/OrderForm'
import type { Symbol, Side } from './components/OrderForm'
import './styles.css'

type HistoryEntry = OrderResult & {
  symbol: Symbol
  side: Side
  qty: string
  px: string
  at: Date
}

export default function App() {
  const [symbol, setSymbol] = useState<Symbol>('PETR4')
  const [side, setSide] = useState<Side>('Buy')
  const [quantity, setQuantity] = useState('')
  const [price, setPrice] = useState('')
  const [loading, setLoading] = useState(false)
  const [connected, setConnected] = useState(false)
  const [result, setResult] = useState<OrderResult | null>(null)
  const [history, setHistory] = useState<HistoryEntry[]>([])

  // Connect to SignalR on mount, disconnect on unmount
  useEffect(() => {
    let mounted = true

    signalRService
      .connect()
      .then(() => {
        if (mounted) setConnected(true)
      })
      .catch(err => {
        console.error('[App] SignalR connect failed:', err)
      })

    signalRService.onConnected(() => {
      if (mounted) setConnected(true)
    })

    // Listen for order status updates pushed from the backend (outbox worker)
    const unsubscribeResult = signalRService.onOrderResult((res) => {
      if (!mounted) return
      console.log('[App] Received real-time update via SignalR:', res)
      if (res.clOrdId) {
        setHistory(prev => prev.map(item => {
          if (item.clOrdId === res.clOrdId) {
            return {
              ...item,
              status: res.status,
              orderId: res.orderId,
              message: res.message,
              success: res.success
            }
          }
          return item
        }))
        // Update the form's current result display if it is showing this same order
        setResult(curr => {
          if (curr?.clOrdId === res.clOrdId) {
            return res
          }
          return curr
        })
      }
    })

    return () => {
      mounted = false
      signalRService.disconnect()
      unsubscribeResult()
    }
  }, [])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setResult(null)

    const qty = parseInt(quantity, 10)
    const px = parseFloat(price)

    // Client-side validation (mirrors server-side rules)
    if (!qty || qty <= 0 || qty >= 100_000) {
      setResult({ success: false, status: 'ERROR', error: 'Quantidade deve ser um inteiro positivo menor que 100.000' })
      setLoading(false)
      return
    }
    if (!px || px <= 0 || px >= 1000 || Math.round(px * 100) / 100 !== px) {
      setResult({ success: false, status: 'ERROR', error: 'Preço deve ser um decimal positivo, múltiplo de 0,01 e menor que 1.000' })
      setLoading(false)
      return
    }

    try {
      const r = await signalRService.submitOrder(symbol, side, qty, px)
      setResult(r)
      setHistory(prev => [{ ...r, symbol, side, qty: quantity, px: price, at: new Date() }, ...prev.slice(0, 9)])
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Erro de conexão com o servidor'
      const r: OrderResult = { success: false, status: 'ERROR', error: message }
      setResult(r)
      setHistory(prev => [{ ...r, symbol, side, qty: quantity, px: price, at: new Date() }, ...prev.slice(0, 9)])
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="app">
      <div className="container">
        {/* Header */}
        <header className="header">
          <div className="header-badge">FIX 4.4 Protocol</div>
          <h1>Order Generator</h1>
          <p className="subtitle">Envio de ordens em tempo real via protocolo FIX</p>
          {/* Connection status indicator */}
          <div className={`conn-status ${connected ? 'online' : 'offline'}`} id="connection-status">
            <span className="conn-dot" />
            {connected ? 'Conectado ao servidor' : 'Aguardando conexão…'}
          </div>
        </header>

        <div className="layout">
          {/* Form Card */}
          <div className="card form-card">
            <div className="card-header">
              <h2>Nova Ordem</h2>
            </div>

            <OrderForm
              symbol={symbol}
              side={side}
              quantity={quantity}
              price={price}
              loading={loading}
              connected={connected}
              onSymbolChange={setSymbol}
              onSideChange={setSide}
              onQuantityChange={setQuantity}
              onPriceChange={setPrice}
              onSubmit={handleSubmit}
              result={result}
            />
          </div>

          {/* History Card */}
          <div className="card history-card">
            <div className="card-header">
              <h2>Histórico</h2>
            </div>
            {/* Exposure limit info */}
            <div className="limit-info">
              <div className="limit-title">Limite de Exposição por Símbolo</div>
              <div className="limit-value">± R$ 100.000.000</div>
              <div className="limit-desc">Ordens que ultrapassem o limite em valor absoluto são rejeitadas pelo OrderAccumulator</div>
            </div>
            
            {history.length === 0 ? (
              <div className="history-empty">
                <div className="history-empty-icon">—</div>
                <p>Nenhuma ordem enviada ainda</p>
              </div>
            ) : (
              <div className="history-list">
                {history.map((h, i) => {
                  return (
                    <div key={i} className={`history-item ${h.status.toLowerCase()}`}>
                      <div className="history-left">
                        <div className="symbol-dot" style={{ background: SYMBOL_META[h.symbol].color }} />
                        <div>
                          <div className="history-symbol">{h.symbol}</div>
                          <div className="history-side" style={{ color: h.side === 'Buy' ? 'var(--green)' : 'var(--red)' }}>
                            {h.side === 'Buy' ? '▲ Compra' : '▼ Venda'} · {parseInt(h.qty).toLocaleString('pt-BR')} unid @ R${parseFloat(h.px).toFixed(2)}
                          </div>
                        </div>
                      </div>
                      <div className="history-right">
                        <div className={`history-badge ${h.status.toLowerCase()}`}>
                          {h.status === 'ACCEPTED' ? '✓' : h.status === 'REJECTED' ? '×' : '··'}
                        </div>
                        <div className="history-time">{h.at.toLocaleTimeString('pt-BR')}</div>
                      </div>
                    </div>
                  )
                })}
              </div>
            )}            
          </div>
        </div>
      </div>
    </div>
  )
}
