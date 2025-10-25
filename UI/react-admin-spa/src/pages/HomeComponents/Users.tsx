import React, { useMemo, useState } from "react";
import {
  Alert,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  InputAdornment,
  MenuItem,
  Paper,
  Snackbar,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
  Checkbox,
  ListItemText
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import PasswordIcon from "@mui/icons-material/Password";
import AddIcon from "@mui/icons-material/Add";
import SearchIcon from "@mui/icons-material/Search";
import { api } from "../../api";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

// Normalize API errors (handles ASP.NET Core ProblemDetails)
function getApiErrorMessage(e: any): string {
  try {
    const data = e?.response?.data;
    if (typeof data === "string") return data;
    if (data && typeof data === "object") {
      const title = typeof (data as any).title === "string" ? (data as any).title : undefined;
      const errors = (data as any).errors;
      if (errors && typeof errors === "object") {
        const firstKey = Object.keys(errors)[0];
        const msgs = (errors as any)[firstKey];
        const msg = Array.isArray(msgs) ? msgs.join(", ") : String(msgs ?? "");
        return title ? `${title}: ${msg}` : msg || title || "Validation error";
      }
      return title || JSON.stringify(data);
    }
    return e?.message || "Unexpected error";
  } catch {
    return "Unexpected error";
  }
}

type UserStatus = "Active" | "Inactive";

type UserDto = {
  id: string;
  userName: string;
  email: string;
  fullName: string;
  phoneNumber?: string | null;
  status: UserStatus;
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;      // ISO
  updatedAt?: string | null;
  roles: string[];
};

type RoleDto = { id: string; name: string };

const statusOptions: UserStatus[] = ["Active", "Inactive"];

const Users: React.FC = () => {
  const queryClient = useQueryClient();
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState<"" | UserStatus>("");

  // ---- create dialog
  const [openCreate, setOpenCreate] = useState(false);
  const [cUserName, setCUserName] = useState("");
  const [cFullName, setCFullName] = useState("");
  const [cPassword, setCPassword] = useState("");
  const [cStatus, setCStatus] = useState<UserStatus>("Active");
  const [cRoles, setCRoles] = useState<string[]>([]);
  const [cError, setCError] = useState<any>(null);

  // ---- change password dialog
  const [openPwd, setOpenPwd] = useState(false);
  const [pUser, setPUser] = useState<UserDto | null>(null);
  const [pPassword, setPPassword] = useState("");
  const [pError, setPError] = useState<any>(null);

  const [snack, setSnack] = useState<string | null>(null);

  const usersQuery = useQuery({
    queryKey: ["users", { query, status }],
    queryFn: async ({ signal }) => {
      const params = new URLSearchParams();
      if (query.trim()) params.set("query", query.trim());
      if (status) params.set("status", status);
      const { data } = await api.get<UserDto[]>("/api/cruduser", { params, signal });
      return data;
    },
  });

  const rolesQuery = useQuery({
    queryKey: ["roles"],
    queryFn: async () => {
      const { data } = await api.get<RoleDto[]>("/api/cruduser/roles");
      return data;
    },
    staleTime: 5 * 60_000,
  });

  const items = usersQuery.data ?? [];
  const loading = usersQuery.isInitialLoading || usersQuery.isFetching;
  const loadError = usersQuery.error as any;
  const listError = loadError ? (loadError?.response?.data ?? loadError?.message ?? "Ошибка загрузки") : null;
  const roles = rolesQuery.data ?? [];

  const cBusy = createUserMutation.isPending;
  const pBusy = changePasswordMutation.isPending;

  const filteredText = useMemo(() => {
    const parts: string[] = [];
    if (query.trim()) parts.push(`поиск="${query.trim()}"`);
    if (status) parts.push(`статус=${status}`);
    return parts.join(", ");
  }, [query, status]);

  // ---- create dialog open (preload roles)
  const openCreateDialog = async () => {
    setCUserName("");
    setCFullName("");
    setCPassword("");
    setCStatus("Active");
    setCRoles([]);
    setCError(null);
    if (!rolesQuery.data) await rolesQuery.refetch();
    setOpenCreate(true);
  };

  const createUserMutation = useMutation({
    mutationFn: async (payload: { userName: string; fullName: string; password: string; status: UserStatus; roles: string[] }) => {
      const res = await api.post<UserDto>(
        "/api/cruduser",
        {
          userName: payload.userName.trim(),
          fullName: payload.fullName.trim(),
          password: payload.password,
          status: payload.status,
          roles: payload.roles,
        },
        { validateStatus: (s) => s === 201, headers: { Accept: "application/json" } }
      );

      if (!res.data?.id) {
        const loc = (res.headers as any)?.location ?? (res.headers as any)?.Location;
        if (typeof loc === "string" && loc) {
          try {
            await api.get<UserDto>(loc);
          } catch {
            // ignore
          }
        }
      }
    },
    onSuccess: async () => {
      setOpenCreate(false);
      setSnack("Пользователь создан");
      await queryClient.invalidateQueries({ queryKey: ["users"] });
    },
    onError: (err: any) => {
      setCError(err?.response?.data ?? err?.message ?? "Ошибка создания");
    },
    onSettled: () => {
      setCPassword("");
    }
  });

  const changePasswordMutation = useMutation({
    mutationFn: async (payload: { id: string; password: string; userName?: string }) => {
      await api.post(`/api/cruduser/${payload.id}/password`, { newPassword: payload.password });
    },
    onSuccess: async (_, variables) => {
      setOpenPwd(false);
      setSnack(`Пароль для ${variables.userName ?? "пользователя"} обновлён`);
      setPPassword("");
      await queryClient.invalidateQueries({ queryKey: ["users"] });
    },
    onError: (err: any) => setPError(err?.response?.data ?? err?.message ?? "Ошибка смены пароля"),
  });

  const submitCreate = async () => {
    // простая фронт-валидация
    if (cUserName.trim().length < 3) { setCError("Логин: минимум 3 символа"); return; }
    if (cFullName.trim().length < 2) { setCError("ФИО: минимум 2 символа"); return; }
    if (cPassword.length < 6) { setCError("Пароль: минимум 6 символов"); return; }

    setCError(null);
    await createUserMutation.mutateAsync({
      userName: cUserName,
      fullName: cFullName,
      password: cPassword,
      status: cStatus,
      roles: cRoles,
    });
  };

  // ---- password dialog
  const openPasswordDialog = (u: UserDto) => {
    setPUser(u);
    setPPassword("");
    setPError(null);
    setOpenPwd(true);
  };

  const submitPassword = async () => {
    if (!pUser) return;
    if (pPassword.length < 6) { setPError("Пароль: минимум 6 символов"); return; }

    setPError(null);
    await changePasswordMutation.mutateAsync({ id: pUser.id, password: pPassword, userName: pUser.userName });
  };

  return (
    <Stack spacing={2}>
      <Stack direction="row" alignItems="center" justifyContent="space-between">
        <Typography variant="h6">Пользователи</Typography>
        <Stack direction="row" spacing={1}>
          <Tooltip title="Обновить">
            <span>
              <IconButton onClick={() => usersQuery.refetch()} disabled={loading}><RefreshIcon /></IconButton>
            </span>
          </Tooltip>
          <Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>
            Создать
          </Button>
        </Stack>
      </Stack>

      <Paper sx={{ p: 2 }}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
          <TextField
            size="small"
            label="Поиск"
            placeholder="логин, ФИО, email, телефон"
            value={query}
            onChange={e => setQuery(e.target.value)}
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" />
                </InputAdornment>
              )
            }}
            sx={{ minWidth: 260 }}
          />
          <TextField
            size="small"
            select
            label="Статус"
            value={status}
            onChange={e => setStatus(e.target.value as any)}
            sx={{ minWidth: 180 }}
          >
            <MenuItem value="">Любой</MenuItem>
            {statusOptions.map(s => (
              <MenuItem key={s} value={s}>{s}</MenuItem>
            ))}
          </TextField>
          <Button variant="outlined" onClick={() => usersQuery.refetch()} disabled={loading} sx={{ minWidth: 140 }}>
            {loading ? <CircularProgress size={20} /> : "Применить"}
          </Button>
        </Stack>
        {filteredText && (
          <Typography variant="caption" color="text.secondary" sx={{ mt: 1, display: "block" }}>
            Фильтры: {filteredText}
          </Typography>
        )}
      </Paper>

      {listError && <Alert severity="error">{String(listError)}</Alert>}

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Логин</TableCell>
              <TableCell>ФИО</TableCell>
              <TableCell>Email</TableCell>
              <TableCell>Роли</TableCell>
              <TableCell>Статус</TableCell>
              <TableCell>Создан</TableCell>
              <TableCell align="right">Действия</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.map(u => (
              <TableRow key={u.id} hover>
                <TableCell>{u.userName}</TableCell>
                <TableCell>{u.fullName}</TableCell>
                <TableCell>{u.email || "—"}</TableCell>
                <TableCell>{u.roles?.length ? u.roles.join(", ") : "—"}</TableCell>
                <TableCell>
                  <Chip
                    size="small"
                    label={u.status}
                    color={u.status === "Active" ? "success" : "default"}
                    variant="outlined"
                  />
                </TableCell>
                <TableCell>{new Date(u.createdAt).toLocaleString()}</TableCell>
                <TableCell align="right">
                  <Tooltip title="Сменить пароль">
                    <IconButton onClick={() => openPasswordDialog(u)}>
                      <PasswordIcon />
                    </IconButton>
                  </Tooltip>
                </TableCell>
              </TableRow>
            ))}
            {!items.length && !loading && (
              <TableRow>
                <TableCell colSpan={7} align="center">Нет данных</TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Create dialog */}
      <Dialog open={openCreate} onClose={() => setOpenCreate(false)} fullWidth maxWidth="sm">
        <DialogTitle>Создать пользователя</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {cError && (
              <Alert severity="error">
                {typeof cError === 'string' ? cError : getApiErrorMessage({ response: { data: cError } })}
              </Alert>
            )}
            <TextField
              label="Логин"
              value={cUserName}
              onChange={e => setCUserName(e.target.value)}
              autoFocus
              required
            />
            <TextField
              label="Полное имя"
              value={cFullName}
              onChange={e => setCFullName(e.target.value)}
              required
            />
            <TextField
              label="Пароль"
              type="password"
              value={cPassword}
              onChange={e => setCPassword(e.target.value)}
              required
            />
            <TextField
              select
              label="Статус"
              value={cStatus}
              onChange={e => setCStatus(e.target.value as UserStatus)}
            >
              {statusOptions.map(s => (
                <MenuItem key={s} value={s}>{s}</MenuItem>
              ))}
            </TextField>

            {/* Roles multiple select */}
            <TextField
              select
              label="Роли"
              value={cRoles}
              onChange={e =>
                setCRoles(
                  typeof e.target.value === "string"
                    ? (e.target.value as string).split(",")
                    : (e.target.value as string[])
                )
              }
              SelectProps={{
                multiple: true,
                renderValue: (selected) => (selected as string[]).join(", ")
              }}
              helperText="Необязательно"
            >
              {roles.map(r => {
                const checked = cRoles.includes(r.name);
                return (
                  <MenuItem key={r.id} value={r.name}>
                    <Checkbox checked={checked} />
                    <ListItemText primary={r.name} />
                  </MenuItem>
                );
              })}
            </TextField>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenCreate(false)}>Отмена</Button>
          <Button onClick={submitCreate} disabled={cBusy} variant="contained">
            {cBusy ? "Создание..." : "Создать"}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Change password dialog */}
      <Dialog open={openPwd} onClose={() => setOpenPwd(false)} fullWidth maxWidth="sm">
        <DialogTitle>Смена пароля {pUser?.userName}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {pError && (
              <Alert severity="error">
                {typeof pError === 'string' ? pError : getApiErrorMessage({ response: { data: pError } })}
              </Alert>
            )}
            <TextField
              label="Новый пароль"
              type="password"
              value={pPassword}
              onChange={e => setPPassword(e.target.value)}
              autoFocus
              required
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenPwd(false)}>Отмена</Button>
          <Button onClick={submitPassword} disabled={pBusy} variant="contained">
            {pBusy ? "Сохранение..." : "Сменить"}
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={!!snack}
        autoHideDuration={2500}
        onClose={() => setSnack(null)}
        message={snack ?? ""}
      />
    </Stack>
  );
};

export default Users;
