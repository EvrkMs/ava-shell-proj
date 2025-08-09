import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { AuthProvider } from "./auth/AuthContext";
import { ColorThemeProvider } from "./theme/ColorThemeProvider";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <BrowserRouter>
    <ColorThemeProvider>
      <AuthProvider>
        <App />
      </AuthProvider>
    </ColorThemeProvider>
  </BrowserRouter>
);
