import { u as useTranslation, Y as useToast, r as reactExports, j as jsxRuntimeExports, a2 as Settings2, B as Button } from "./index-CBGGrI33.js";
import { C as Card, a as CardHeader, b as CardTitle, c as CardContent } from "./card-B5CbN4-5.js";
import { L as Label, S as Switch, I as Input } from "./label-mNw1xWB1.js";
import "./slider-DbaiTU_V.js";
import "./select--OXYUWzT.js";
import "./table-CycXrrfD.js";
import { T as Textarea } from "./textarea-TaHjrfbF.js";
import { L as Layers } from "./layers-UBAoXSQT.js";
import "./index-DOD0wY2v.js";
import "./index-DZePc0M4.js";
function SessionManagement$1() {
  const { t } = useTranslation();
  const { toast } = useToast();
  const [config, setConfig] = reactExports.useState({
    mode: "single",
    sessionTimeout: 30,
    maxMessagesPerSession: 50,
    deleteAfterTimeout: false,
    maxSessionsPerAccount: 3
  });
  const [hasChanges, setHasChanges] = reactExports.useState(false);
  reactExports.useEffect(() => {
    loadConfig();
  }, []);
  const loadConfig = async () => {
    try {
      const sessionConfig = await window.electronAPI.session.getConfig();
      setConfig(sessionConfig);
    } catch (error) {
      console.error("Failed to load session config:", error);
    }
  };
  const handleConfigChange = (updates) => {
    setConfig((prev) => ({ ...prev, ...updates }));
    setHasChanges(true);
  };
  const saveConfig = async () => {
    try {
      await window.electronAPI.session.updateConfig(config);
      setHasChanges(false);
      toast({
        title: t("common.success"),
        description: t("session.configSaved")
      });
    } catch (error) {
      toast({
        title: t("common.error"),
        description: t("session.configSaveFailed"),
        variant: "destructive"
      });
    }
  };
  return /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "space-y-6", children: /* @__PURE__ */ jsxRuntimeExports.jsxs(Card, { children: [
    /* @__PURE__ */ jsxRuntimeExports.jsx(CardHeader, { children: /* @__PURE__ */ jsxRuntimeExports.jsxs(CardTitle, { className: "flex items-center gap-2", children: [
      /* @__PURE__ */ jsxRuntimeExports.jsx(Settings2, { className: "h-5 w-5" }),
      t("session.sessionSettings")
    ] }) }),
    /* @__PURE__ */ jsxRuntimeExports.jsxs(CardContent, { className: "space-y-6", children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between p-4 bg-muted/30 rounded-lg", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-0.5", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { htmlFor: "delete-after-chat", children: t("session.deleteAfterChat") }),
          /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-sm text-muted-foreground", children: t("session.deleteAfterChatHint") })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(
          Switch,
          {
            id: "delete-after-chat",
            checked: config.deleteAfterTimeout,
            onCheckedChange: (checked) => handleConfigChange({ deleteAfterTimeout: checked })
          }
        )
      ] }),
      hasChanges && /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "flex justify-end pt-4 border-t", children: /* @__PURE__ */ jsxRuntimeExports.jsx(Button, { onClick: saveConfig, children: t("common.save") }) })
    ] })
  ] }) });
}
function ContextManagement() {
  const { t } = useTranslation();
  const { toast } = useToast();
  const [config, setConfig] = reactExports.useState({
    enabled: false,
    strategies: {
      slidingWindow: { enabled: true, maxMessages: 20 },
      tokenLimit: { enabled: false, maxTokens: 4e3 },
      summary: { enabled: false, keepRecentMessages: 20 }
    },
    executionOrder: ["slidingWindow", "tokenLimit", "summary"]
  });
  const [hasChanges, setHasChanges] = reactExports.useState(false);
  reactExports.useEffect(() => {
    loadConfig();
  }, []);
  const loadConfig = async () => {
    try {
      const contextConfig = await window.electronAPI.contextManagement.getConfig();
      setConfig(contextConfig);
    } catch (error) {
      console.error("Failed to load context management config:", error);
    }
  };
  const handleConfigChange = (updates) => {
    setConfig((prev) => ({ ...prev, ...updates }));
    setHasChanges(true);
  };
  const handleStrategyChange = (strategyKey, updates) => {
    setConfig((prev) => ({
      ...prev,
      strategies: {
        ...prev.strategies,
        [strategyKey]: {
          ...prev.strategies[strategyKey],
          ...updates
        }
      }
    }));
    setHasChanges(true);
  };
  const saveConfig = async () => {
    try {
      await window.electronAPI.contextManagement.updateConfig(config);
      setHasChanges(false);
      toast({
        title: t("common.success"),
        description: t("contextManagement.configSaved")
      });
    } catch (error) {
      toast({
        title: t("common.error"),
        description: t("contextManagement.configSaveFailed"),
        variant: "destructive"
      });
    }
  };
  return /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "space-y-6", children: /* @__PURE__ */ jsxRuntimeExports.jsxs(Card, { children: [
    /* @__PURE__ */ jsxRuntimeExports.jsx(CardHeader, { children: /* @__PURE__ */ jsxRuntimeExports.jsxs(CardTitle, { className: "flex items-center gap-2", children: [
      /* @__PURE__ */ jsxRuntimeExports.jsx(Layers, { className: "h-5 w-5" }),
      t("contextManagement.title")
    ] }) }),
    /* @__PURE__ */ jsxRuntimeExports.jsxs(CardContent, { className: "space-y-6", children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between p-4 bg-muted/30 rounded-lg", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-0.5", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { htmlFor: "context-enabled", children: t("contextManagement.enableContextManagement") }),
          /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-sm text-muted-foreground", children: t("contextManagement.enableContextManagementHint") })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(
          Switch,
          {
            id: "context-enabled",
            checked: config.enabled,
            onCheckedChange: (checked) => handleConfigChange({ enabled: checked })
          }
        )
      ] }),
      config.enabled && /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-4 pt-4 border-t", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "h-2 w-2 rounded-full bg-primary" }),
          /* @__PURE__ */ jsxRuntimeExports.jsx("span", { className: "text-sm font-medium text-muted-foreground", children: t("contextManagement.strategyConfig") })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-4", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "p-4 border rounded-lg space-y-4", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between", children: [
              /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-0.5", children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { className: "font-medium", children: t("contextManagement.slidingWindow.title") }),
                /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-sm text-muted-foreground", children: t("contextManagement.slidingWindow.description") })
              ] }),
              /* @__PURE__ */ jsxRuntimeExports.jsx(
                Switch,
                {
                  checked: config.strategies.slidingWindow.enabled,
                  onCheckedChange: (checked) => handleStrategyChange("slidingWindow", { enabled: checked })
                }
              )
            ] }),
            config.strategies.slidingWindow.enabled && /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "flex items-center gap-4 pl-4 border-l-2", children: /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex-1 space-y-2", children: [
              /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { htmlFor: "max-messages", children: t("contextManagement.slidingWindow.maxMessages") }),
              /* @__PURE__ */ jsxRuntimeExports.jsx(
                Input,
                {
                  id: "max-messages",
                  type: "number",
                  min: 1,
                  max: 100,
                  value: config.strategies.slidingWindow.maxMessages,
                  onChange: (e) => handleStrategyChange("slidingWindow", {
                    maxMessages: parseInt(e.target.value) || 20
                  }),
                  className: "w-32"
                }
              ),
              /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs text-muted-foreground", children: t("contextManagement.slidingWindow.maxMessagesHint") })
            ] }) })
          ] }),
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "p-4 border rounded-lg space-y-4", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between", children: [
              /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-0.5", children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { className: "font-medium", children: t("contextManagement.tokenLimit.title") }),
                /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-sm text-muted-foreground", children: t("contextManagement.tokenLimit.description") })
              ] }),
              /* @__PURE__ */ jsxRuntimeExports.jsx(
                Switch,
                {
                  checked: config.strategies.tokenLimit.enabled,
                  onCheckedChange: (checked) => handleStrategyChange("tokenLimit", { enabled: checked })
                }
              )
            ] }),
            config.strategies.tokenLimit.enabled && /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "flex items-center gap-4 pl-4 border-l-2", children: /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex-1 space-y-2", children: [
              /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { htmlFor: "max-tokens", children: t("contextManagement.tokenLimit.maxTokens") }),
              /* @__PURE__ */ jsxRuntimeExports.jsx(
                Input,
                {
                  id: "max-tokens",
                  type: "number",
                  min: 100,
                  max: 1e5,
                  value: config.strategies.tokenLimit.maxTokens,
                  onChange: (e) => handleStrategyChange("tokenLimit", {
                    maxTokens: parseInt(e.target.value) || 4e3
                  }),
                  className: "w-32"
                }
              ),
              /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs text-muted-foreground", children: t("contextManagement.tokenLimit.maxTokensHint") })
            ] }) })
          ] }),
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "p-4 border rounded-lg space-y-4", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between", children: [
              /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-0.5", children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { className: "font-medium", children: t("contextManagement.summary.title") }),
                /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-sm text-muted-foreground", children: t("contextManagement.summary.description") })
              ] }),
              /* @__PURE__ */ jsxRuntimeExports.jsx(
                Switch,
                {
                  checked: config.strategies.summary.enabled,
                  onCheckedChange: (checked) => handleStrategyChange("summary", { enabled: checked })
                }
              )
            ] }),
            config.strategies.summary.enabled && /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-4 pl-4 border-l-2", children: [
              /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-2", children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { htmlFor: "keep-recent", children: t("contextManagement.summary.keepRecentMessages") }),
                /* @__PURE__ */ jsxRuntimeExports.jsx(
                  Input,
                  {
                    id: "keep-recent",
                    type: "number",
                    min: 1,
                    max: 100,
                    value: config.strategies.summary.keepRecentMessages,
                    onChange: (e) => handleStrategyChange("summary", {
                      keepRecentMessages: parseInt(e.target.value) || 20
                    }),
                    className: "w-32"
                  }
                ),
                /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs text-muted-foreground", children: t("contextManagement.summary.keepRecentMessagesHint") })
              ] }),
              /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-2", children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { htmlFor: "summary-prompt", children: t("contextManagement.summary.customPrompt") }),
                /* @__PURE__ */ jsxRuntimeExports.jsx(
                  Textarea,
                  {
                    id: "summary-prompt",
                    placeholder: t("contextManagement.summary.customPromptPlaceholder"),
                    value: config.strategies.summary.summaryPrompt || "",
                    onChange: (e) => handleStrategyChange("summary", { summaryPrompt: e.target.value }),
                    rows: 3
                  }
                ),
                /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs text-muted-foreground", children: t("contextManagement.summary.customPromptHint") })
              ] })
            ] })
          ] })
        ] })
      ] }),
      hasChanges && /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "flex justify-end pt-4 border-t", children: /* @__PURE__ */ jsxRuntimeExports.jsx(Button, { onClick: saveConfig, children: t("common.save") }) })
    ] })
  ] }) });
}
function SessionManagement() {
  const { t } = useTranslation();
  return /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-6", children: [
    /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsx("h2", { className: "text-2xl font-bold tracking-tight", children: t("session.title") }),
      /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-muted-foreground", children: t("session.description") })
    ] }),
    /* @__PURE__ */ jsxRuntimeExports.jsx(SessionManagement$1, {}),
    /* @__PURE__ */ jsxRuntimeExports.jsx(ContextManagement, {})
  ] });
}
export {
  SessionManagement,
  SessionManagement as default
};
