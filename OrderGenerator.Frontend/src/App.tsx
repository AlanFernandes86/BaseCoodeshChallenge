import React, { useState } from 'react'

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000'

type Symbol = 'PETR4' | 'VALE3' | 'VIIA4'
type Side = 'Buy' | 'Sell'

interface OrderResult {
  success: boolean
  status: 'ACCEPTED' | 'REJECTED' | 'ERROR'
  orderId?: string
  clOrdId?: string
  message?: string
  error?: string
  timestamp?: string
}

const SYMBOLS: Symbol[] = ['PETR4', 'VALE3', 'VIIA4']

const SYMBOL_META: Record<Symbol, { color: string; emoji: string; name: string }> = {
  PETR4: { color: '#f97316', emoji: '🛢️', name: 'Petrobras' },
  VALE3: { color: '#22c55e', emoji: '⛏️', name: 'Vale' },
  VIIA4: { color: '#8b5cf6', emoji: '🛍️', name: 'Via Varejo' },
}

export default function App() {
  const [symbol, setSymbol] = useState<Symbol>('PETR4')
  const [side, setSide] = useState<Side>('Buy')
  const [quantity, setQuantity] = useState('')
  const [price, setPrice] = useState('')
  const [loading, setLoading] = useState(false)
  const [result, setResult] = useState<OrderResult | null>(null)
  const [history, setHistory] = useState<(OrderResult & { symbol: Symbol; side: Side; qty: string; px: string; at: Date })[]>([])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setResult(null)

    const qty = parseInt(quantity, 10)
    const px = parseFloat(price)

    // Client-side validation
    if (!qty || qty <= 0 || qty >= 100_000) {
      setResult({ success: false, status: 'ERROR', error: 'Quantidade deve ser um inteiro positivo menor que 100.000' })
      setLoading(false)
      return
    }
    if (!px || px <= 0 || px >= 1000 || (Math.round(px * 100) / 100) !== px) {
      setResult({ success: false, status: 'ERROR', error: 'Preço deve ser um decimal positivo, múltiplo de 0,01 e menor que 1.000' })
      setLoading(false)
      return
    }

    try {
      const resp = await fetch(`${API_URL}/api/orders/submit`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ symbol, side, quantity: qty, price: px }),
      })

      const data = await resp.json()

      if (resp.ok) {
        const r: OrderResult = {
          success: true,
          status: 'ACCEPTED',
          orderId: data.orderId,
          clOrdId: data.clOrdId,
          message: data.message,
          timestamp: data.timestamp,
        }
        setResult(r)
        setHistory(prev => [{ ...r, symbol, side, qty: quantity, px: price, at: new Date() }, ...prev.slice(0, 9)])
      } else {
        const r: OrderResult = {
          success: false,
          status: data.status === 'REJECTED' ? 'REJECTED' : 'ERROR',
          clOrdId: data.clOrdId,
          message: data.message,
          error: data.error,
          timestamp: data.timestamp,
        }
        setResult(r)
        setHistory(prev => [{ ...r, symbol, side, qty: quantity, px: price, at: new Date() }, ...prev.slice(0, 9)])
      }
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Erro de conexão com o servidor'
      setResult({ success: false, status: 'ERROR', error: message })
    } finally {
      setLoading(false)
    }
  }


  return (
    <div className="app">
      {/* Background orbs */}
      <div className="orb orb-1" />
      <div className="orb orb-2" />
      <div className="orb orb-3" />

      <div className="container">
        {/* Header */}
        <header className="header">
          <div className="header-badge">FIX 4.4 Protocol</div>
          <h1>Order Generator</h1>
          <p className="subtitle">Envio de ordens em tempo real via protocolo FIX</p>
        </header>

        <div className="layout">
          {/* Form Card */}
          <div className="card form-card">
            <div className="card-header">
              <span className="card-icon">📝</span>
              <h2>Nova Ordem</h2>
            </div>

            <form onSubmit={handleSubmit} id="order-form">
              {/* Symbol */}
              <div className="field-group">
                <label htmlFor="symbol">Símbolo</label>
                <div className="symbol-grid">
                  {SYMBOLS.map(s => {
                    const m = SYMBOL_META[s]
                    return (
                      <button
                        key={s}
                        id={`symbol-${s}`}
                        type="button"
                        className={`symbol-btn ${symbol === s ? 'selected' : ''}`}
                        style={{ '--symbol-color': m.color } as React.CSSProperties}
                        onClick={() => setSymbol(s)}
                      >
                        <span className="symbol-emoji">{m.emoji}</span>
                        <span className="symbol-ticker">{s}</span>
                        <span className="symbol-name">{m.name}</span>
                      </button>
                    )
                  })}
                </div>
              </div>

              {/* Side */}
              <div className="field-group">
                <label htmlFor="side">Lado</label>
                <div className="side-toggle">
                  <button
                    id="side-buy"
                    type="button"
                    className={`side-btn buy ${side === 'Buy' ? 'selected' : ''}`}
                    onClick={() => setSide('Buy')}
                  >
                    <span>▲</span> Compra
                  </button>
                  <button
                    id="side-sell"
                    type="button"
                    className={`side-btn sell ${side === 'Sell' ? 'selected' : ''}`}
                    onClick={() => setSide('Sell')}
                  >
                    <span>▼</span> Venda
                  </button>
                </div>
              </div>

              {/* Quantity */}
              <div className="field-group">
                <label htmlFor="quantity">Quantidade</label>
                <div className="input-wrapper">
                  <input
                    id="quantity"
                    type="number"
                    min="1"
                    max="99999"
                    step="1"
                    placeholder="Ex: 1000"
                    value={quantity}
                    onChange={e => setQuantity(e.target.value)}
                    required
                  />
                  <span className="input-hint">1 – 99.999 unidades</span>
                </div>
              </div>

              {/* Price */}
              <div className="field-group">
                <label htmlFor="price">Preço (R$)</label>
                <div className="input-wrapper">
                  <input
                    id="price"
                    type="number"
                    min="0.01"
                    max="999.99"
                    step="0.01"
                    placeholder="Ex: 35.50"
                    value={price}
                    onChange={e => setPrice(e.target.value)}
                    required
                  />
                  <span className="input-hint">0,01 – 999,99 · múltiplo de 0,01</span>
                </div>
              </div>

              {/* Total preview */}
              {quantity && price && (
                <div className="total-preview">
                  <span>Exposição estimada:</span>
                  <strong style={{ color: side === 'Buy' ? '#4ade80' : '#f87171' }}>
                    {side === 'Buy' ? '+' : '-'}
                    {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(
                      parseFloat(quantity) * parseFloat(price)
                    )}
                  </strong>
                </div>
              )}

              <button id="submit-order" type="submit" className="submit-btn" disabled={loading}>
                {loading ? (
                  <><span className="spinner" /> Enviando via FIX…</>
                ) : (
                  <>⚡ Enviar Ordem</>
                )}
              </button>
            </form>

            {/* Result */}
            {result && (
              <div className={`result-box ${result.status.toLowerCase()}`} id="order-result">
                <div className="result-icon">
                  {result.status === 'ACCEPTED' ? '✅' : result.status === 'REJECTED' ? '❌' : '⚠️'}
                </div>
                <div className="result-content">
                  <div className="result-status">
                    {result.status === 'ACCEPTED' ? 'Ordem Aceita' : result.status === 'REJECTED' ? 'Ordem Rejeitada' : 'Erro'}
                  </div>
                  {result.orderId && <div className="result-detail">Order ID: <code>{result.orderId}</code></div>}
                  {result.clOrdId && <div className="result-detail">Client Order ID: <code>{result.clOrdId}</code></div>}
                  {result.message && <div className="result-message">{result.message}</div>}
                  {result.error && <div className="result-message">{result.error}</div>}
                </div>
              </div>
            )}
          </div>

          {/* History Card */}
          <div className="card history-card">
            <div className="card-header">
              <span className="card-icon">📊</span>
              <h2>Histórico</h2>
            </div>

            {history.length === 0 ? (
              <div className="history-empty">
                <span>📭</span>
                <p>Nenhuma ordem enviada ainda</p>
              </div>
            ) : (
              <div className="history-list">
                {history.map((h, i) => {
                  const hMeta = SYMBOL_META[h.symbol]
                  return (
                    <div key={i} className={`history-item ${h.status.toLowerCase()}`}>
                      <div className="history-left">
                        <span className="history-emoji">{hMeta.emoji}</span>
                        <div>
                          <div className="history-symbol">{h.symbol}</div>
                          <div className="history-side" style={{ color: h.side === 'Buy' ? '#4ade80' : '#f87171' }}>
                            {h.side === 'Buy' ? '▲ Compra' : '▼ Venda'} · {parseInt(h.qty).toLocaleString('pt-BR')} unid @ R$ {parseFloat(h.px).toFixed(2)}
                          </div>
                        </div>
                      </div>
                      <div className="history-right">
                        <div className={`history-badge ${h.status.toLowerCase()}`}>
                          {h.status === 'ACCEPTED' ? '✓' : '✗'}
                        </div>
                        <div className="history-time">{h.at.toLocaleTimeString('pt-BR')}</div>
                      </div>
                    </div>
                  )
                })}
              </div>
            )}

            {/* Exposure limit info */}
            <div className="limit-info">
              <div className="limit-title">Limite de Exposição por Símbolo</div>
              <div className="limit-value">± R$ 100.000.000</div>
              <div className="limit-desc">Ordens que ultrapassem o limite em valor absoluto são rejeitadas pelo OrderAccumulator</div>
            </div>
          </div>
        </div>
      </div>

      <style>{`
        *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

        body {
          font-family: 'Inter', system-ui, -apple-system, sans-serif;
          background: #0a0f1e;
          color: #e2e8f0;
          min-height: 100vh;
          overflow-x: hidden;
        }

        .app {
          min-height: 100vh;
          position: relative;
          overflow: hidden;
        }

        /* Animated background orbs */
        .orb {
          position: fixed;
          border-radius: 50%;
          filter: blur(80px);
          opacity: 0.15;
          pointer-events: none;
          animation: drift 20s ease-in-out infinite;
        }
        .orb-1 { width: 600px; height: 600px; background: #3b82f6; top: -200px; left: -200px; animation-delay: 0s; }
        .orb-2 { width: 500px; height: 500px; background: #8b5cf6; bottom: -150px; right: -100px; animation-delay: -7s; }
        .orb-3 { width: 400px; height: 400px; background: #06b6d4; top: 40%; left: 40%; animation-delay: -14s; }

        @keyframes drift {
          0%, 100% { transform: translate(0, 0) scale(1); }
          33% { transform: translate(30px, -30px) scale(1.05); }
          66% { transform: translate(-20px, 20px) scale(0.95); }
        }

        .container {
          max-width: 1100px;
          margin: 0 auto;
          padding: 2rem 1.5rem 4rem;
          position: relative;
          z-index: 1;
        }

        /* Header */
        .header {
          text-align: center;
          margin-bottom: 3rem;
        }
        .header-badge {
          display: inline-block;
          background: rgba(59, 130, 246, 0.2);
          border: 1px solid rgba(59, 130, 246, 0.4);
          color: #60a5fa;
          font-size: 0.75rem;
          font-weight: 600;
          letter-spacing: 0.1em;
          text-transform: uppercase;
          padding: 0.3rem 1rem;
          border-radius: 999px;
          margin-bottom: 1rem;
        }
        .header h1 {
          font-size: clamp(2rem, 5vw, 3.5rem);
          font-weight: 800;
          background: linear-gradient(135deg, #60a5fa 0%, #c084fc 50%, #34d399 100%);
          -webkit-background-clip: text;
          -webkit-text-fill-color: transparent;
          background-clip: text;
          margin-bottom: 0.75rem;
          letter-spacing: -0.02em;
        }
        .subtitle {
          color: #64748b;
          font-size: 1rem;
        }

        /* Layout */
        .layout {
          display: grid;
          grid-template-columns: 1fr 1fr;
          gap: 1.5rem;
          align-items: start;
        }
        @media (max-width: 768px) {
          .layout { grid-template-columns: 1fr; }
        }

        /* Card */
        .card {
          background: rgba(15, 23, 42, 0.8);
          border: 1px solid rgba(255,255,255,0.08);
          border-radius: 20px;
          padding: 1.75rem;
          backdrop-filter: blur(20px);
          box-shadow: 0 20px 60px rgba(0,0,0,0.4);
        }
        .card-header {
          display: flex;
          align-items: center;
          gap: 0.75rem;
          margin-bottom: 1.75rem;
          padding-bottom: 1rem;
          border-bottom: 1px solid rgba(255,255,255,0.07);
        }
        .card-icon { font-size: 1.25rem; }
        .card-header h2 {
          font-size: 1.1rem;
          font-weight: 700;
          color: #cbd5e1;
          letter-spacing: -0.01em;
        }

        /* Form fields */
        .field-group {
          margin-bottom: 1.5rem;
        }
        label {
          display: block;
          font-size: 0.8rem;
          font-weight: 600;
          color: #64748b;
          text-transform: uppercase;
          letter-spacing: 0.08em;
          margin-bottom: 0.6rem;
        }

        /* Symbol buttons */
        .symbol-grid {
          display: grid;
          grid-template-columns: repeat(3, 1fr);
          gap: 0.75rem;
        }
        .symbol-btn {
          background: rgba(255,255,255,0.03);
          border: 1px solid rgba(255,255,255,0.08);
          border-radius: 12px;
          padding: 0.875rem 0.5rem;
          cursor: pointer;
          color: #94a3b8;
          transition: all 0.2s ease;
          display: flex;
          flex-direction: column;
          align-items: center;
          gap: 0.25rem;
        }
        .symbol-btn:hover {
          border-color: var(--symbol-color);
          background: color-mix(in srgb, var(--symbol-color) 10%, transparent);
          color: #e2e8f0;
          transform: translateY(-2px);
        }
        .symbol-btn.selected {
          border-color: var(--symbol-color);
          background: color-mix(in srgb, var(--symbol-color) 20%, transparent);
          color: #fff;
          box-shadow: 0 0 20px color-mix(in srgb, var(--symbol-color) 30%, transparent);
        }
        .symbol-emoji { font-size: 1.25rem; }
        .symbol-ticker { font-size: 0.85rem; font-weight: 700; letter-spacing: 0.03em; }
        .symbol-name { font-size: 0.65rem; opacity: 0.7; }

        /* Side toggle */
        .side-toggle {
          display: grid;
          grid-template-columns: 1fr 1fr;
          gap: 0.75rem;
        }
        .side-btn {
          background: rgba(255,255,255,0.03);
          border: 1px solid rgba(255,255,255,0.08);
          border-radius: 12px;
          padding: 0.875rem;
          cursor: pointer;
          color: #94a3b8;
          font-size: 0.95rem;
          font-weight: 600;
          transition: all 0.2s ease;
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 0.5rem;
        }
        .side-btn.buy.selected {
          border-color: #22c55e;
          background: rgba(34, 197, 94, 0.15);
          color: #4ade80;
          box-shadow: 0 0 20px rgba(34, 197, 94, 0.2);
        }
        .side-btn.sell.selected {
          border-color: #ef4444;
          background: rgba(239, 68, 68, 0.15);
          color: #f87171;
          box-shadow: 0 0 20px rgba(239, 68, 68, 0.2);
        }
        .side-btn:hover:not(.selected) {
          border-color: rgba(255,255,255,0.2);
          background: rgba(255,255,255,0.06);
          color: #e2e8f0;
        }

        /* Inputs */
        .input-wrapper {
          position: relative;
        }
        input[type="number"] {
          width: 100%;
          background: rgba(255,255,255,0.04);
          border: 1px solid rgba(255,255,255,0.1);
          border-radius: 12px;
          padding: 0.875rem 1rem;
          color: #e2e8f0;
          font-size: 1rem;
          font-family: inherit;
          transition: all 0.2s ease;
          -moz-appearance: textfield;
        }
        input[type="number"]::-webkit-outer-spin-button,
        input[type="number"]::-webkit-inner-spin-button { -webkit-appearance: none; }
        input[type="number"]:focus {
          outline: none;
          border-color: #3b82f6;
          background: rgba(59, 130, 246, 0.08);
          box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.15);
        }
        input::placeholder { color: #334155; }
        .input-hint {
          display: block;
          font-size: 0.7rem;
          color: #475569;
          margin-top: 0.4rem;
          padding-left: 0.25rem;
        }

        /* Total preview */
        .total-preview {
          background: rgba(255,255,255,0.04);
          border: 1px solid rgba(255,255,255,0.08);
          border-radius: 10px;
          padding: 0.75rem 1rem;
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 1.5rem;
          font-size: 0.85rem;
          color: #64748b;
        }
        .total-preview strong { font-size: 1rem; }

        /* Submit button */
        .submit-btn {
          width: 100%;
          background: linear-gradient(135deg, #3b82f6, #8b5cf6);
          border: none;
          border-radius: 12px;
          padding: 1rem;
          color: #fff;
          font-size: 1rem;
          font-weight: 700;
          cursor: pointer;
          transition: all 0.2s ease;
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 0.5rem;
          letter-spacing: 0.02em;
          position: relative;
          overflow: hidden;
        }
        .submit-btn::before {
          content: '';
          position: absolute;
          inset: 0;
          background: linear-gradient(135deg, #60a5fa, #a78bfa);
          opacity: 0;
          transition: opacity 0.2s;
        }
        .submit-btn:hover:not(:disabled)::before { opacity: 1; }
        .submit-btn:active:not(:disabled) { transform: scale(0.98); }
        .submit-btn:disabled {
          opacity: 0.6;
          cursor: not-allowed;
        }
        .submit-btn > * { position: relative; }

        /* Spinner */
        .spinner {
          width: 16px; height: 16px;
          border: 2px solid rgba(255,255,255,0.3);
          border-top-color: #fff;
          border-radius: 50%;
          animation: spin 0.7s linear infinite;
          display: inline-block;
        }
        @keyframes spin { to { transform: rotate(360deg); } }

        /* Result box */
        .result-box {
          margin-top: 1.25rem;
          border-radius: 12px;
          padding: 1rem 1.25rem;
          display: flex;
          gap: 1rem;
          align-items: flex-start;
          animation: slideIn 0.3s ease;
        }
        @keyframes slideIn {
          from { opacity: 0; transform: translateY(8px); }
          to { opacity: 1; transform: translateY(0); }
        }
        .result-box.accepted {
          background: rgba(34, 197, 94, 0.1);
          border: 1px solid rgba(34, 197, 94, 0.3);
        }
        .result-box.rejected {
          background: rgba(239, 68, 68, 0.1);
          border: 1px solid rgba(239, 68, 68, 0.3);
        }
        .result-box.error {
          background: rgba(245, 158, 11, 0.1);
          border: 1px solid rgba(245, 158, 11, 0.3);
        }
        .result-icon { font-size: 1.5rem; flex-shrink: 0; margin-top: 0.1rem; }
        .result-status {
          font-weight: 700;
          font-size: 0.95rem;
          margin-bottom: 0.35rem;
        }
        .result-box.accepted .result-status { color: #4ade80; }
        .result-box.rejected .result-status { color: #f87171; }
        .result-box.error .result-status { color: #fbbf24; }
        .result-detail {
          font-size: 0.78rem;
          color: #64748b;
          margin-bottom: 0.2rem;
        }
        .result-detail code {
          font-family: 'Fira Mono', monospace;
          color: #94a3b8;
          background: rgba(255,255,255,0.06);
          padding: 0.1rem 0.3rem;
          border-radius: 4px;
        }
        .result-message {
          font-size: 0.8rem;
          color: #94a3b8;
          margin-top: 0.35rem;
          line-height: 1.5;
        }

        /* History */
        .history-empty {
          text-align: center;
          padding: 2.5rem 0;
          color: #334155;
        }
        .history-empty span { font-size: 2rem; display: block; margin-bottom: 0.5rem; }
        .history-empty p { font-size: 0.85rem; }

        .history-list { display: flex; flex-direction: column; gap: 0.6rem; margin-bottom: 1.5rem; }
        .history-item {
          display: flex;
          justify-content: space-between;
          align-items: center;
          background: rgba(255,255,255,0.03);
          border: 1px solid rgba(255,255,255,0.06);
          border-radius: 10px;
          padding: 0.75rem 1rem;
          animation: slideIn 0.25s ease;
        }
        .history-left { display: flex; gap: 0.75rem; align-items: center; }
        .history-emoji { font-size: 1.1rem; }
        .history-symbol { font-size: 0.85rem; font-weight: 700; color: #cbd5e1; }
        .history-side { font-size: 0.72rem; margin-top: 0.15rem; }
        .history-right { display: flex; flex-direction: column; align-items: flex-end; gap: 0.2rem; }
        .history-badge {
          width: 22px; height: 22px;
          border-radius: 50%;
          display: flex; align-items: center; justify-content: center;
          font-size: 0.7rem; font-weight: 700;
        }
        .history-badge.accepted { background: rgba(34,197,94,0.2); color: #4ade80; }
        .history-badge.rejected { background: rgba(239,68,68,0.2); color: #f87171; }
        .history-badge.error { background: rgba(245,158,11,0.2); color: #fbbf24; }
        .history-time { font-size: 0.68rem; color: #475569; }

        /* Limit info */
        .limit-info {
          background: rgba(59, 130, 246, 0.06);
          border: 1px solid rgba(59, 130, 246, 0.15);
          border-radius: 12px;
          padding: 1rem;
          margin-top: 1rem;
        }
        .limit-title { font-size: 0.72rem; color: #64748b; text-transform: uppercase; letter-spacing: 0.07em; font-weight: 600; margin-bottom: 0.3rem; }
        .limit-value { font-size: 1.1rem; font-weight: 700; color: #60a5fa; margin-bottom: 0.35rem; }
        .limit-desc { font-size: 0.73rem; color: #475569; line-height: 1.5; }
      `}</style>
    </div>
  )
}
