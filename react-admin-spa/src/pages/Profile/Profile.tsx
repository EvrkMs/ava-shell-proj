import React from "react";
import { Stack, Typography, Tabs, Tab, Box } from "@mui/material";
import { PersonalCard, TelegramCard } from "./components";
import SessionsCard from "./components/SessionsCard";

const Profile: React.FC = () => {
  const [tab, setTab] = React.useState(0);
  return (
    <Stack spacing={3}>
      <Typography variant="h4">Профиль</Typography>
      <PersonalCard />

      <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
        <Tabs value={tab} onChange={(_, v) => setTab(v)} aria-label="profile tabs">
          <Tab label="Telegram" />
          <Tab label="Сессии" />
        </Tabs>
      </Box>
      <Box role="tabpanel" hidden={tab !== 0}>{tab === 0 && <TelegramCard />}</Box>
      <Box role="tabpanel" hidden={tab !== 1}>{tab === 1 && <SessionsCard />}</Box>
    </Stack>
  );
};

export default Profile;
