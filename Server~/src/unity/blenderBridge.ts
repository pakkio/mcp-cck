import WebSocket from 'ws';
import { Logger } from '../utils/logger.js';
import { v4 as uuidv4 } from 'uuid';

export enum BlenderConnectionState {
    Disconnected = 'disconnected',
    Connecting = 'connecting',
    Connected = 'connected',
    Reconnecting = 'reconnecting'
}

interface PendingRequest {
    resolve: (value: any) => void;
    reject: (reason: any) => void;
    method: string;
    timeout: NodeJS.Timeout;
}

export class BlenderBridge {
    private ws: WebSocket | null = null;
    private state: BlenderConnectionState = BlenderConnectionState.Disconnected;
    private pendingRequests: Map<string, PendingRequest> = new Map();
    private reconnectAttempt = 0;
    private maxReconnectAttempts = 50;
    private minReconnectDelay = 1000;
    private maxReconnectDelay = 30000;
    private reconnectMultiplier = 2;
    private requestTimeout = 15000;

    constructor(
        private logger: Logger,
        private host: string = '127.0.0.1',
        private port: number = 9876
    ) {}

    get url(): string {
        return `ws://${this.host}:${this.port}`;
    }

    get connectionState(): BlenderConnectionState {
        return this.state;
    }

    get isConnected(): boolean {
        return this.state === BlenderConnectionState.Connected && this.ws?.readyState === WebSocket.OPEN;
    }

    async connect(): Promise<void> {
        if (this.isConnected) return;
        this.state = BlenderConnectionState.Connecting;
        this.reconnectAttempt++;
        this.logger.info(`Connecting to Blender bridge at ${this.url} (attempt ${this.reconnectAttempt})`);

        return new Promise((resolve, reject) => {
            try {
                this.ws = new WebSocket(this.url, { handshakeTimeout: 5000 });
                const connTimeout = setTimeout(() => {
                    if (this.state !== BlenderConnectionState.Connected) {
                        this.ws?.terminate();
                        reject(new Error('Connection timeout'));
                    }
                }, 5000);

                this.ws.on('open', () => {
                    clearTimeout(connTimeout);
                    this.state = BlenderConnectionState.Connected;
                    this.reconnectAttempt = 0;
                    this.logger.info(`Connected to Blender bridge at ${this.url}`);
                    resolve();
                });

                this.ws.on('message', (data: WebSocket.Data) => {
                    this.handleMessage(data.toString());
                });

                this.ws.on('close', (code: number, reason: Buffer) => {
                    this.state = BlenderConnectionState.Disconnected;
                    this.rejectAllPending('Connection to Blender bridge was closed');
                    if (code !== 1000) this.scheduleReconnect();
                });

                this.ws.on('error', (err: Error) => {
                    clearTimeout(connTimeout);
                    this.logger.error(`Blender bridge error: ${err.message}`);
                    this.state = BlenderConnectionState.Disconnected;
                    this.rejectAllPending(`Connection error: ${err.message}`);
                    reject(err);
                });
            } catch (err) {
                this.state = BlenderConnectionState.Disconnected;
                reject(err);
            }
        });
    }

    private handleMessage(raw: string): void {
        try {
            const msg = JSON.parse(raw);
            if (!msg.id) return;
            const pending = this.pendingRequests.get(msg.id);
            if (!pending) return;
            clearTimeout(pending.timeout);
            this.pendingRequests.delete(msg.id);
            if (msg.error) {
                pending.reject(new Error(msg.error.message || 'Blender bridge error'));
            } else {
                pending.resolve(msg.result);
            }
        } catch (err) {
            this.logger.warn(`Failed to parse Blender bridge message: ${err}`);
        }
    }

    private rejectAllPending(reason: string): void {
        for (const [, pending] of this.pendingRequests) {
            clearTimeout(pending.timeout);
            pending.reject(new Error(reason));
        }
        this.pendingRequests.clear();
    }

    private scheduleReconnect(): void {
        if (this.reconnectAttempt >= this.maxReconnectAttempts) {
            this.logger.error('Max Blender reconnect attempts reached');
            return;
        }
        this.state = BlenderConnectionState.Reconnecting;
        const delay = Math.min(
            this.minReconnectDelay * Math.pow(this.reconnectMultiplier, this.reconnectAttempt - 1),
            this.maxReconnectDelay
        );
        setTimeout(() => { this.connect().catch(() => {}); }, delay);
    }

    async sendRequest(method: string, params: any, timeoutMs?: number): Promise<any> {
        if (!this.isConnected) throw new Error('Not connected to Blender bridge');
        const id = uuidv4();
        const timeout = timeoutMs || this.requestTimeout;

        return new Promise((resolve, reject) => {
            const timeoutHandle = setTimeout(() => {
                this.pendingRequests.delete(id);
                reject(new Error(`Timed out waiting for '${method}' response`));
            }, timeout);

            this.pendingRequests.set(id, { resolve, reject, method, timeout: timeoutHandle });
            this.ws!.send(JSON.stringify({ id, method, params }), (err) => {
                if (err) {
                    clearTimeout(timeoutHandle);
                    this.pendingRequests.delete(id);
                    reject(err);
                }
            });
        });
    }

    async disconnect(): Promise<void> {
        this.rejectAllPending('Disconnecting');
        if (this.ws) { this.ws.close(1000, 'Normal close'); this.ws = null; }
        this.state = BlenderConnectionState.Disconnected;
    }
}
