import React from "react";
import { useAuth } from "../auth/AuthContext";
import { Link } from "react-router-dom";
import {
  Box,
  Button,
  Paper,
  Tab,
  Tabs,
  Typography,
  Stack,
  Divider,
} from "@mui/material";

function a11yProps(index: number) {
  return {
    id: `home-tab-${index}`,
    "aria-controls": `home-tabpanel-${index}`,
  };
}

const TabPanel: React.FC<{
  children?: React.ReactNode;
  index: number;
  value: number;
}> = ({ children, value, index }) => {
  return (
    <div
      role="tabpanel"
      hidden={value !== index}
      id={`home-tabpanel-${index}`}
      aria-labelledby={`home-tab-${index}`}
    >
      {value === index && <Box sx={{ pt: 2 }}>{children}</Box>}
    </div>
  );
};

const UsersStub: React.FC = () => {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack spacing={1.5}>
        <Typography variant="h6">Список пользователей</Typography>
        <Typography color="text.secondary">
          Здесь будет таблица пользователей (поиск, фильтры, роли, пагинация).
        </Typography>
        <Divider />
        <Typography variant="body2" color="text.secondary">
          Статус: в разработке. Заглушка интерфейса.
        </Typography>
      </Stack>
    </Paper>
  );
};

const Home: React.FC = () => {
  const { state } = useAuth();
  const [tab, setTab] = React.useState(0);

  return (
    <Box>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <Box>
          <Typography variant="h4" gutterBottom>Главная</Typography>
        </Box>
        {!state.isAuthenticated && (
          <Button variant="outlined" component={Link} to="/login">
            Войти
          </Button>
        )}
      </Stack>

      <Paper sx={{ width: "100%" }} variant="outlined">
        <Tabs
          value={tab}
          onChange={(_, v) => setTab(v)}
          aria-label="home tabs"
          variant="scrollable"
          scrollButtons="auto"
        >
          <Tab label="Пользователи" {...a11yProps(0)} />
          {/* будущие вкладки:
             <Tab label="Роли" {...a11yProps(1)} />
             <Tab label="Аудит" {...a11yProps(2)} />
          */}
        </Tabs>

        <Box sx={{ p: 2 }}>
          <TabPanel value={tab} index={0}>
            <UsersStub />
          </TabPanel>
        </Box>
      </Paper>
    </Box>
  );
};

export default Home;
