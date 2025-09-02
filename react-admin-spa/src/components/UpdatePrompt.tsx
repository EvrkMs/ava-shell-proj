import React from "react";
import { useRegisterSW } from "virtual:pwa-register/react";
import { Snackbar, Alert, Button, Stack } from "@mui/material";

const UpdatePrompt: React.FC = () => {
  const {
    needRefresh: [needRefresh, setNeedRefresh],
    offlineReady: [offlineReady, setOfflineReady],
    updateServiceWorker,
  } = useRegisterSW({
    onRegisteredSW() {
      // no-op
    },
  });

  const handleReload = () => updateServiceWorker(true);
  const handleClose = () => setNeedRefresh(false);

  return (
    <>
      {/* Show one-time offline ready info (optional) */}
      <Snackbar
        open={offlineReady}
        autoHideDuration={2500}
        onClose={() => setOfflineReady(false)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity="success" onClose={() => setOfflineReady(false)}>
          Приложение готово к офлайн-работе.
        </Alert>
      </Snackbar>

      {/* Show update available prompt */}
      <Snackbar
        open={needRefresh}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          severity="info"
          action={
            <Stack direction="row" spacing={1}>
              <Button color="inherit" size="small" onClick={handleReload}>
                Обновить
              </Button>
              <Button color="inherit" size="small" onClick={handleClose}>
                Позже
              </Button>
            </Stack>
          }
        >
          Доступна новая версия приложения.
        </Alert>
      </Snackbar>
    </>
  );
};

export default UpdatePrompt;

