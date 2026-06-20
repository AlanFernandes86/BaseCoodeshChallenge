import * as signalR from '@microsoft/signalr'

const HUB_URL = (import.meta.env.VITE_API_URL || 'http://localhost:5000') + '/orderhub'

export interface OrderResult {
  success: boolean
  status: 'ACCEPTED' | 'REJECTED' | 'ERROR' | 'PENDING'
  orderId?: string
  clOrdId?: string
  message?: string
  error?: string
  timestamp?: string
}

type OrderResultCallback = (result: OrderResult) => void
type ConnectedCallback = (connectionId: string) => void

class SignalRService {
  private connection: signalR.HubConnection | null = null
  private resultCallbacks: OrderResultCallback[] = []
  private connectedCallbacks: ConnectedCallback[] = []

  /** Build the connection (does not start it). */
  private buildConnection(): signalR.HubConnection {
    return new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()
  }

  /** Connect to the hub. Safe to call multiple times. */
  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) return

    this.connection = this.buildConnection()

    this.connection.on('Connected', (connectionId: string) => {
      console.log('[SignalR] Connected:', connectionId)
      this.connectedCallbacks.forEach(cb => cb(connectionId))
    })

    this.connection.on('OrderResult', (result: OrderResult) => {
      console.log('[SignalR] OrderResult:', result)
      this.resultCallbacks.forEach(cb => cb(result))
    })

    this.connection.onreconnecting(() => {
      console.warn('[SignalR] Reconnecting…')
    })

    this.connection.onreconnected(() => {
      console.log('[SignalR] Reconnected')
    })

    this.connection.onclose(() => {
      console.warn('[SignalR] Connection closed')
    })

    await this.connection.start()
    console.log('[SignalR] Started. State:', this.connection.state)
  }

  /** Disconnect from the hub. */
  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop()
      this.connection = null
    }
  }

  /**
   * Invoke SubmitOrder on the hub and return the execution result.
   * Wraps the event-based pattern in a Promise for ergonomic use.
   */
  async submitOrder(
    symbol: string,
    side: 'Buy' | 'Sell',
    quantity: number,
    price: number,
  ): Promise<OrderResult> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR não está conectado. Aguarde a conexão e tente novamente.')
    }

    return new Promise<OrderResult>((resolve, reject) => {
      // Register a one-shot listener for the result
      const handler = (result: OrderResult) => {
        const idx = this.resultCallbacks.indexOf(handler)
        if (idx !== -1) this.resultCallbacks.splice(idx, 1)
        resolve(result)
      }
      this.resultCallbacks.push(handler)

      // Send order to hub
      this.connection!.invoke('SubmitOrder', symbol, side, quantity, price).catch(err => {
        const idx = this.resultCallbacks.indexOf(handler)
        if (idx !== -1) this.resultCallbacks.splice(idx, 1)
        reject(err)
      })
    })
  }

  /** Register a callback for every OrderResult event (for real-time updates outside of submitOrder). */
  onOrderResult(cb: OrderResultCallback): () => void {
    this.resultCallbacks.push(cb)
    return () => {
      const idx = this.resultCallbacks.indexOf(cb)
      if (idx !== -1) this.resultCallbacks.splice(idx, 1)
    }
  }

  /** Register a callback for when the connection is established. */
  onConnected(cb: ConnectedCallback): void {
    this.connectedCallbacks.push(cb)
  }

  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected
  }

  get connectionState(): string {
    return this.connection?.state ?? 'Disconnected'
  }
}

// Singleton instance
export const signalRService = new SignalRService()
