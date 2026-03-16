/**
 * Server configuration — mirrors Assets/CompanionScan/Core/ServerConfig.cs.
 *
 * Resolution order for baseUrl:
 *   1. localStorage key `CompanionScan.ServerBaseUrl`
 *   2. Constructor argument / default
 */
export class ServerConfig {
  static readonly LocalStorageKey = 'CompanionScan.ServerBaseUrl';

  baseUrl: string;
  boxThreshold: number;
  textThreshold: number;
  requestTimeoutMs: number;
  retryCount: number;
  retryDelayMs: number;

  constructor(opts: Partial<ServerConfig> = {}) {
    this.baseUrl = opts.baseUrl ?? ServerConfig.getDefaultBaseUrl();
    this.boxThreshold = opts.boxThreshold ?? 0.35;
    this.textThreshold = opts.textThreshold ?? 0.25;
    this.requestTimeoutMs = opts.requestTimeoutMs ?? 6000;
    this.retryCount = opts.retryCount ?? 2;
    this.retryDelayMs = opts.retryDelayMs ?? 500;
  }

  private static getDefaultBaseUrl(): string {
    if (typeof window !== 'undefined') {
      const { origin, protocol } = window.location;
      if (protocol === 'http:' || protocol === 'https:') {
        return origin;
      }
    }
    return 'http://localhost:8000';
  }

  /** Returns the effective base URL, giving localStorage the highest priority. */
  resolveBaseUrl(): string {
    const stored = localStorage.getItem(ServerConfig.LocalStorageKey);
    if (stored && stored.trim()) return stored.trim();
    return this.baseUrl;
  }

  /** Persist a custom base URL override into localStorage. */
  setBaseUrlOverride(url: string): void {
    if (url && url.trim()) {
      localStorage.setItem(ServerConfig.LocalStorageKey, url.trim());
    } else {
      localStorage.removeItem(ServerConfig.LocalStorageKey);
    }
  }
}
