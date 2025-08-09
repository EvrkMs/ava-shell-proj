import React, { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthContext";

const STORAGE_KEY = "post_login_return_url";

const Callback: React.FC = () => {
  const { completeSignin } = useAuth();
  const [err, setErr] = useState<string>();
  const ranRef = useRef(false);

  useEffect(() => {
    if (ranRef.current) return; // защитимся от двойного вызова эффекта
    ranRef.current = true;

    (async () => {
      try {
        await completeSignin(); // разбирает code/state и кладёт юзера в стор
        const sp = new URLSearchParams(window.location.search);
        const raw = sessionStorage.getItem(STORAGE_KEY) || sp.get("returnUrl") || "/profile";
        let ret = "/profile";
        try { ret = raw ? decodeURIComponent(raw) : "/profile"; } catch { ret = raw; }
        
        sessionStorage.removeItem(STORAGE_KEY);
        window.location.replace(ret);
        // Используем жесткий переход, чтобы гарантировать чистый URL без code/state
      } catch (e: any) {
        setErr(String(e?.message ?? e));
      }
    })();
  }, [completeSignin]);

  return <div style={{ padding: 24 }}>{err ? `Ошибка: ${err}` : "Завершаем вход..."}</div>;
};

export default Callback;
