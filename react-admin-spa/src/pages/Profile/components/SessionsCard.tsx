import React from "react";
import { Paper, Stack, Typography, Button, Collapse, IconButton } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import { api } from "../../../api";
import { useAuth } from "../../../auth/AuthContext";
import { decodeJwtClaims } from "../../../utils/jwt";

type Session = {
  id: string;
  device?: string | null;
  userAgent?: string | null;
  ipAddress?: string | null;
  createdAt: string;
  lastSeenAt?: string | null;
  expiresAt?: string | null;
  revoked: boolean;
  revokedAt?: string | null;
};

const SessionsCard: React.FC = () => {
  const [current, setCurrent] = React.useState<Session | null>(null);
  const [list, setList] = React.useState<Session[]>([]);
  const [expanded, setExpanded] = React.useState(false);
  const [busy, setBusy] = React.useState(false);
  const { signout, state } = useAuth();

  const normalizeGuidN = (id?: string) => (id || "").replace(/-/g, "").toLowerCase();

  const load = async () => {
    // 1) Список активных сессий
    let items: Session[] = [];
    try {
      const res = await api.get<Session[]>("/api/sessions", { params: { all: false } });
      items = res.data;
      setList(items);
    } catch (e) {
      setList([]);
      items = [];
    }

    // 2) Текущая сессия: сервер или fallback по sid из access_token
    try {
      const cur = await api.get<Session>("/api/sessions/current");
      setCurrent(cur.data);
      return;
    } catch {}

    try {
      // Access token may be a reference token (opaque). Prefer id_token for JWT claims.
      const claims = decodeJwtClaims(state.idToken) || decodeJwtClaims(state.accessToken);
      const sidN = normalizeGuidN(String(claims?.sid || claims?.["sid"]) || "");
      if (sidN && items.length) {
        const match = items.find(x => normalizeGuidN(x.id) === sidN);
        setCurrent(match || null);
      } else {
        setCurrent(null);
      }
    } catch {
      setCurrent(null);
    }
  };

  React.useEffect(() => { load(); }, []);

  const revokeAll = async () => {
    try {
      setBusy(true);
      await api.post("/api/sessions/revoke-all");
      await signout();
    } finally { setBusy(false); }
  };
  const revokeOne = async (id: string) => {
    try {
      setBusy(true);
      await api.post(`/api/sessions/${id}/revoke`);
      const currentIdN = normalizeGuidN(current?.id);
      const targetIdN = normalizeGuidN(id);
      const isCurrent = !!currentIdN && currentIdN === targetIdN;
      if (isCurrent) {
        await signout();
      } else {
        await load();
      }
    } finally { setBusy(false); }
  };

  const other = list.filter(s => !current || s.id.toLowerCase() !== (current?.id || "").toLowerCase());

  return (
    <Paper sx={{ p: 2 }}>
      <Stack spacing={2}>
        <Stack direction="row" justifyContent="space-between" alignItems="center">
          <Typography variant="h6">Сессии</Typography>
          <Button variant="outlined" color="warning" onClick={revokeAll} disabled={busy}>Закрыть все сессии</Button>
        </Stack>

        <Typography variant="subtitle2">Текущая сессия</Typography>
        {current ? (
          <SessionRow s={current} onRevoke={() => revokeOne(current.id)} />
        ) : (
          <Typography color="text.secondary">Не удалось определить текущую сессию</Typography>
        )}

        <Stack direction="row" alignItems="center" spacing={1}>
          <Typography variant="subtitle2">Другие устройства</Typography>
          <IconButton size="small" onClick={() => setExpanded(v => !v)}>
            {expanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" /> }
          </IconButton>
        </Stack>
        <Collapse in={expanded}>
          <Stack spacing={1}>
            {other.length ? other.map(s => (
              <SessionRow key={s.id} s={s} onRevoke={() => revokeOne(s.id)} />
            )) : <Typography color="text.secondary">Нет других активных сессий</Typography>}
          </Stack>
        </Collapse>
      </Stack>
    </Paper>
  );
};

const SessionRow: React.FC<{ s: Session; onRevoke: () => void; disableRevoke?: boolean }> = ({ s, onRevoke, disableRevoke }) => {
  return (
    <Stack direction={{ xs: "column", sm: "row" }} spacing={1} alignItems={{ xs: "flex-start", sm: "center" }} justifyContent="space-between" sx={{ p: 1, border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
      <Stack spacing={0.5}>
        <Typography variant="body2">Устройство: {s.device || "-"}</Typography>
        <Typography variant="body2" color="text.secondary">IP: {s.ipAddress || "-"}</Typography>
        <Typography variant="body2" color="text.secondary">UA: {s.userAgent?.slice(0,120) || "-"}</Typography>
        <Typography variant="caption" color="text.secondary">Создана: {new Date(s.createdAt).toLocaleString()}</Typography>
        {s.lastSeenAt && <Typography variant="caption" color="text.secondary">Последняя активность: {new Date(s.lastSeenAt).toLocaleString()}</Typography>}
        {s.revoked && <Typography variant="caption" color="error">Отозвана{s.revokedAt ? `: ${new Date(s.revokedAt).toLocaleString()}` : ""}</Typography>}
      </Stack>
      <Button variant="text" color="warning" onClick={onRevoke} disabled={disableRevoke || s.revoked}>Закрыть данную сессию</Button>
    </Stack>
  );
};

export default SessionsCard;
