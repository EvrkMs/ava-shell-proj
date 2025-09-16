// utils/safeStorage.ts
export type SafeStore = {
    getItem(key: string): string | null;
    setItem(key: string, val: string): void;
    removeItem(key: string): void;
  };
  
  const memory: Record<string, string> = {};
  const memoryStore: SafeStore = {
    getItem: k => (k in memory ? memory[k] : null),
    setItem: (k, v) => { memory[k] = v; },
    removeItem: k => { delete memory[k]; },
  };
  
  function wrapStorage(s?: Storage | null): SafeStore {
    if (!s) return memoryStore;
    try {
      const testKey = "__test__" + Math.random();
      s.setItem(testKey, "1");
      s.removeItem(testKey);
      return {
        getItem: (k) => {
          try { return s.getItem(k); } catch { return null; }
        },
        setItem: (k, v) => { try { s.setItem(k, v); } catch {} },
        removeItem: (k) => { try { s.removeItem(k); } catch {} },
      };
    } catch {
      return memoryStore;
    }
  }
  
  export const safeLocal = wrapStorage(typeof window !== "undefined" ? window.localStorage : undefined);
  export const safeSession = wrapStorage(typeof window !== "undefined" ? window.sessionStorage : undefined);
  