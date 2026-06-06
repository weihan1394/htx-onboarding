import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import "./index.css";

// mounts React into <div id="root"> in index.html
// StrictMode renders twice in dev to surface side-effects
ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
