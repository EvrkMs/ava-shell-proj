import React, { useEffect } from "react";
import { useAuth } from "../auth/AuthContext";

const LogoutCallback: React.FC = () => {
  const { completeSignout } = useAuth();
  useEffect(() => {
    completeSignout().finally(() => window.location.replace("/"));
  }, [completeSignout]);
  return <div style={{ padding: 24 }}>Выход...</div>;
};

export default LogoutCallback;
