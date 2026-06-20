
export interface OrderResult {
  success: boolean
  status: 'ACCEPTED' | 'REJECTED' | 'ERROR' | 'PENDING'
  orderId?: string
  clOrdId?: string
  message?: string
  error?: string
  timestamp?: string
}

interface OrderResultProps {
  result: OrderResult
}

export function OrderResultPanel({ result }: OrderResultProps) {
  return (
    <div className={`result-box ${result.status.toLowerCase()}`} id="order-result">
      <div className="result-icon">
        {result.status === 'ACCEPTED'
          ? '✅'
          : result.status === 'REJECTED'
            ? '❌'
            : result.status === 'PENDING'
              ? '⏳'
              : '⚠️'}
      </div>
      <div className="result-content">
        <div className="result-status">
          {result.status === 'ACCEPTED'
            ? 'Ordem Aceita'
            : result.status === 'REJECTED'
              ? 'Ordem Rejeitada'
              : result.status === 'PENDING'
                ? 'Ordem Pendente (Fila Outbox)'
                : 'Erro'}
        </div>
        {result.orderId && (
          <div className="result-detail">
            Order ID: <code>{result.orderId}</code>
          </div>
        )}
        {result.clOrdId && (
          <div className="result-detail">
            Client Order ID: <code>{result.clOrdId}</code>
          </div>
        )}
        {result.message && <div className="result-message">{result.message}</div>}
        {result.error && <div className="result-message">{result.error}</div>}
      </div>
    </div>
  )
}
