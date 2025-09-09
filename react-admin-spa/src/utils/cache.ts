export async function resetAppCacheThenReload(): Promise<void> {
  try {
    if ("serviceWorker" in navigator) {
      const regs = await navigator.serviceWorker.getRegistrations();
      await Promise.allSettled(regs.map(r => r.unregister()));
    }
  } catch {}

  try {
    if ("caches" in window) {
      const names = await caches.keys();
      await Promise.allSettled(names.map(n => caches.delete(n)));
    }
  } catch {}

  try { await new Promise(res => setTimeout(res, 100)); } catch {}
  location.reload();
}
