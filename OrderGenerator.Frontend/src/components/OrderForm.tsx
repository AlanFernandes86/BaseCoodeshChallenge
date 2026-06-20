import React from 'react'
import type { OrderResult } from './OrderResult'

export type Symbol = 'PETR4' | 'VALE3' | 'VIIA4'
export type Side = 'Buy' | 'Sell'

export const SYMBOLS: Symbol[] = ['PETR4', 'VALE3', 'VIIA4']

export const SYMBOL_META: Record<Symbol, { color: string; emoji: string; name: string }> = {
  PETR4: { color: '#f97316', emoji: '🛢️', name: 'Petrobras' },
  VALE3: { color: '#22c55e', emoji: '⛏️', name: 'Vale' },
  VIIA4: { color: '#8b5cf6', emoji: '🛍️', name: 'Via Varejo' },
}

interface OrderFormProps {
  symbol: Symbol
  side: Side
  quantity: string
  price: string
  loading: boolean
  connected: boolean
  onSymbolChange: (s: Symbol) => void
  onSideChange: (s: Side) => void
  onQuantityChange: (v: string) => void
  onPriceChange: (v: string) => void
  onSubmit: (e: React.FormEvent) => void
  result: OrderResult | null
}

export function OrderForm({
  symbol,
  side,
  quantity,
  price,
  loading,
  connected,
  onSymbolChange,
  onSideChange,
  onQuantityChange,
  onPriceChange,
  onSubmit,
  result,
}: OrderFormProps) {
  const qty = parseFloat(quantity)
  const px = parseFloat(price)
  const hasPreview = !isNaN(qty) && !isNaN(px) && qty > 0 && px > 0

  return (
    <form onSubmit={onSubmit} id="order-form">
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
                onClick={() => onSymbolChange(s)}
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
        <label>Lado</label>
        <div className="side-toggle">
          <button
            id="side-buy"
            type="button"
            className={`side-btn buy ${side === 'Buy' ? 'selected' : ''}`}
            onClick={() => onSideChange('Buy')}
          >
            <span>▲</span> Compra
          </button>
          <button
            id="side-sell"
            type="button"
            className={`side-btn sell ${side === 'Sell' ? 'selected' : ''}`}
            onClick={() => onSideChange('Sell')}
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
            onChange={e => onQuantityChange(e.target.value)}
            required
          />
          <span className="input-hint">1 – 99.999 unidades inteiras</span>
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
            onChange={e => onPriceChange(e.target.value)}
            required
          />
          <span className="input-hint">0,01 – 999,99 · múltiplo de 0,01</span>
        </div>
      </div>

      {/* Total preview */}
      {hasPreview && (
        <div className="total-preview">
          <span>Exposição estimada:</span>
          <strong style={{ color: side === 'Buy' ? '#4ade80' : '#f87171' }}>
            {side === 'Buy' ? '+' : '-'}
            {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(qty * px)}
          </strong>
        </div>
      )}

      <button
        id="submit-order"
        type="submit"
        className="submit-btn"
        disabled={loading || !connected}
      >
        {loading ? (
          <><span className="spinner" /> Enviando via FIX…</>
        ) : !connected ? (
          <><span className="spinner" /> Conectando ao servidor…</>
        ) : (
          <>⚡ Enviar Ordem</>
        )}
      </button>

      {/* Result */}
      {result && (
        <div className={`result-box ${result.status.toLowerCase()}`} id="order-result">
          <div className="result-icon">
            {result.status === 'ACCEPTED' ? '✅' : result.status === 'REJECTED' ? '❌' : '⚠️'}
          </div>
          <div className="result-content">
            <div className="result-status">
              {result.status === 'ACCEPTED'
                ? 'Ordem Aceita'
                : result.status === 'REJECTED'
                  ? 'Ordem Rejeitada'
                  : 'Erro'}
            </div>
            {result.orderId && (
              <div className="result-detail">Order ID: <code>{result.orderId}</code></div>
            )}
            {result.clOrdId && (
              <div className="result-detail">Client Order ID: <code>{result.clOrdId}</code></div>
            )}
            {result.message && <div className="result-message">{result.message}</div>}
            {result.error && <div className="result-message">{result.error}</div>}
          </div>
        </div>
      )}
    </form>
  )
}
