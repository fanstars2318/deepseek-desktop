import { c as createLucideIcon, u as useTranslation, Y as useToast, r as reactExports, j as jsxRuntimeExports, S as Server, B as Button, M as Dialog, N as DialogContent, O as DialogHeader, Q as DialogTitle, T as DialogDescription, U as DialogFooter, J as Info, f as Square, P as Play, a as cn, a1 as Cpu, a2 as Settings2, K as Settings } from "./index-D0SZLoBs.js";
import { T as Tabs, a as TabsList, b as TabsTrigger, d as TabsContent } from "./tabs-BLE6Tgxv.js";
import { C as Card, a as CardHeader, b as CardTitle, d as CardDescription, c as CardContent } from "./card-D91IwTW3.js";
import { L as Label, I as Input, S as Switch } from "./label-Bsp4YPI2.js";
import { B as Badge } from "./badge-B5LOy72V.js";
import { u as useProxyStore, S as Slider } from "./slider-Dhg_uCn1.js";
import { C as CircleAlert } from "./circle-alert-Ct_RwkrP.js";
import { C as CircleCheck } from "./circle-check-BHeiMp05.js";
import { R as RefreshCw } from "./refresh-cw-CnBquwWi.js";
import { S as Select, b as SelectTrigger, d as SelectValue, e as SelectContent, f as SelectItem } from "./select-8L0mVoPG.js";
import "./table-WqSScJFw.js";
import { T as TrendingUp, A as Activity } from "./trending-up-ejte5_zs.js";
import { C as CircleX } from "./circle-x-CD5FmkMk.js";
import { C as ChartColumn } from "./chart-column-o8aabTbr.js";
import "./index-DnAGkne6.js";
import "./index-B0LmiZ-6.js";
/**
 * @license lucide-react v0.454.0 - ISC
 *
 * This source code is licensed under the ISC license.
 * See the LICENSE file in the root directory of this source tree.
 */
const Scale = createLucideIcon("Scale", [
  ["path", { d: "m16 16 3-8 3 8c-.87.65-1.92 1-3 1s-2.13-.35-3-1Z", key: "7g6ntu" }],
  ["path", { d: "m2 16 3-8 3 8c-.87.65-1.92 1-3 1s-2.13-.35-3-1Z", key: "ijws7r" }],
  ["path", { d: "M7 21h10", key: "1b0cd5" }],
  ["path", { d: "M12 3v18", key: "108xh3" }],
  ["path", { d: "M3 7h2c2 0 5-1 7-2 2 1 5 2 7 2h2", key: "3gwbw2" }]
]);
/**
 * @license lucide-react v0.454.0 - ISC
 *
 * This source code is licensed under the ISC license.
 * See the LICENSE file in the root directory of this source tree.
 */
const Timer = createLucideIcon("Timer", [
  ["line", { x1: "10", x2: "14", y1: "2", y2: "2", key: "14vaq8" }],
  ["line", { x1: "12", x2: "15", y1: "14", y2: "11", key: "17fdiu" }],
  ["circle", { cx: "12", cy: "14", r: "8", key: "1e1u0o" }]
]);
function ProxyConfigForm({ onConfigChange }) {
  const { t } = useTranslation();
  const { proxyConfig, proxyStatus, setProxyConfig, saveAppConfig, startProxy, stopProxy, isLoading } = useProxyStore();
  const { toast } = useToast();
  const initialConfigRef = reactExports.useRef({
    port: proxyConfig.port.toString(),
    host: proxyConfig.host,
    enableCors: proxyConfig.enableCors,
    corsOrigin: proxyConfig.corsOrigin
  });
  const [formData, setFormData] = reactExports.useState({
    port: proxyConfig.port.toString(),
    host: proxyConfig.host,
    enableCors: proxyConfig.enableCors,
    corsOrigin: proxyConfig.corsOrigin
  });
  const [errors, setErrors] = reactExports.useState({});
  const [hasChanges, setHasChanges] = reactExports.useState(false);
  const [showRestartDialog, setShowRestartDialog] = reactExports.useState(false);
  const [isRestarting, setIsRestarting] = reactExports.useState(false);
  reactExports.useEffect(() => {
    const newFormData = {
      port: proxyConfig.port.toString(),
      host: proxyConfig.host,
      enableCors: proxyConfig.enableCors,
      corsOrigin: proxyConfig.corsOrigin
    };
    setFormData(newFormData);
    initialConfigRef.current = newFormData;
  }, []);
  const validatePort = (value) => {
    const port = parseInt(value, 10);
    if (isNaN(port)) return t("proxy.portMustBeNumber");
    if (port < 1 || port > 65535) return t("proxy.portRangeError");
    if (port < 1024 && port !== 80 && port !== 443) {
      return t("proxy.portPrivilege");
    }
    return void 0;
  };
  const validateHost = (value) => {
    if (!value.trim()) return t("proxy.bindAddressEmpty");
    const ipv4Regex = /^(\d{1,3}\.){3}\d{1,3}$/;
    const ipv6Regex = /^\[?[0-9a-fA-F:]+\]?$/;
    const hostnameRegex = /^[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?$/;
    if (!ipv4Regex.test(value) && !ipv6Regex.test(value) && !hostnameRegex.test(value) && value !== "localhost") {
      return t("proxy.invalidIpOrHostname");
    }
    return void 0;
  };
  const validateCorsOrigin = (value) => {
    if (!formData.enableCors) return void 0;
    if (!value.trim()) return t("proxy.corsOriginEmpty");
    if (value !== "*") {
      const origins = value.split(",").map((o) => o.trim());
      for (const origin of origins) {
        try {
          new URL(origin);
        } catch {
          return `${t("proxy.invalidUrl")}: ${origin}`;
        }
      }
    }
    return void 0;
  };
  const handlePortChange = (value) => {
    setFormData((prev) => ({ ...prev, port: value }));
    const error = validatePort(value);
    setErrors((prev) => ({ ...prev, port: error }));
    setHasChanges(true);
    onConfigChange?.();
  };
  const handleHostChange = (value) => {
    setFormData((prev) => ({ ...prev, host: value }));
    const error = validateHost(value);
    setErrors((prev) => ({ ...prev, host: error }));
    setHasChanges(true);
    onConfigChange?.();
  };
  const handleCorsToggle = (enabled) => {
    setFormData((prev) => ({ ...prev, enableCors: enabled }));
    if (enabled) {
      const error = validateCorsOrigin(formData.corsOrigin);
      setErrors((prev) => ({ ...prev, corsOrigin: error }));
    } else {
      setErrors((prev) => ({ ...prev, corsOrigin: void 0 }));
    }
    setHasChanges(true);
    onConfigChange?.();
  };
  const handleCorsOriginChange = (value) => {
    setFormData((prev) => ({ ...prev, corsOrigin: value }));
    const error = validateCorsOrigin(value);
    setErrors((prev) => ({ ...prev, corsOrigin: error }));
    setHasChanges(true);
    onConfigChange?.();
  };
  const handleSave = async () => {
    const portError = validatePort(formData.port);
    const hostError = validateHost(formData.host);
    const corsError = validateCorsOrigin(formData.corsOrigin);
    if (portError || hostError || corsError) {
      setErrors({
        port: portError,
        host: hostError,
        corsOrigin: corsError
      });
      toast({
        title: t("proxy.validationFailed"),
        description: t("proxy.fixFormErrors"),
        variant: "destructive"
      });
      return;
    }
    const newPort = parseInt(formData.port, 10);
    const newHost = formData.host;
    const portOrHostChanged = newPort !== proxyConfig.port || newHost !== proxyConfig.host;
    const isProxyRunning = proxyStatus?.isRunning;
    if (portOrHostChanged && isProxyRunning) {
      setShowRestartDialog(true);
      return;
    }
    await performSave(newPort, newHost);
  };
  const performSave = async (newPort, newHost) => {
    const newConfig = {
      port: newPort,
      host: newHost,
      enableCors: formData.enableCors,
      corsOrigin: formData.corsOrigin
    };
    setProxyConfig(newConfig);
    const success = await saveAppConfig({
      proxyPort: newConfig.port,
      proxyHost: newConfig.host
    });
    if (success) {
      setHasChanges(false);
      initialConfigRef.current = {
        port: newPort.toString(),
        host: newHost,
        enableCors: formData.enableCors,
        corsOrigin: formData.corsOrigin
      };
      toast({
        title: t("common.success"),
        description: t("proxy.proxyConfigUpdated")
      });
    } else {
      toast({
        title: t("common.error"),
        description: t("proxy.configSaveFailed"),
        variant: "destructive"
      });
    }
  };
  const handleRestartAndSave = async () => {
    setIsRestarting(true);
    try {
      const newPort = parseInt(formData.port, 10);
      const newHost = formData.host;
      await performSave(newPort, newHost);
      await stopProxy();
      await new Promise((resolve) => setTimeout(resolve, 500));
      const success = await startProxy(newPort);
      if (success) {
        toast({
          title: t("common.success"),
          description: t("proxy.proxyRestarted")
        });
      } else {
        toast({
          title: t("common.error"),
          description: t("proxy.proxyRestartFailed"),
          variant: "destructive"
        });
      }
    } finally {
      setIsRestarting(false);
      setShowRestartDialog(false);
    }
  };
  const handleReset = () => {
    setFormData(initialConfigRef.current);
    setErrors({});
    setHasChanges(false);
  };
  const isValid = !errors.port && !errors.host && !errors.corsOrigin;
  return /* @__PURE__ */ jsxRuntimeExports.jsxs(jsxRuntimeExports.Fragment, { children: [
    /* @__PURE__ */ jsxRuntimeExports.jsxs(Card, { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs(CardHeader, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsx(Server, { className: "h-5 w-5 text-primary" }),
            /* @__PURE__ */ jsxRuntimeExports.jsx(CardTitle, { children: t("proxy.basicConfig") })
          ] }),
          hasChanges && /* @__PURE__ */ jsxRuntimeExports.jsx(Badge, { variant: "secondary", className: "text-xs", children: t("proxy.unsaved") })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(CardDescription, { children: t("proxy.basicConfigDesc") })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsxs(CardContent, { className: "space-y-6", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "grid gap-4 md:grid-cols-2", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-2", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs(Label, { htmlFor: "port", className: "flex items-center gap-2", children: [
              t("proxy.listeningPort"),
              errors.port && /* @__PURE__ */ jsxRuntimeExports.jsxs("span", { className: "text-destructive text-xs flex items-center gap-1", children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx(CircleAlert, { className: "h-3 w-3" }),
                errors.port
              ] }),
              !errors.port && formData.port && /* @__PURE__ */ jsxRuntimeExports.jsx(CircleCheck, { className: "h-3 w-3 text-green-500" })
            ] }),
            /* @__PURE__ */ jsxRuntimeExports.jsx(
              Input,
              {
                id: "port",
                type: "number",
                placeholder: "8080",
                value: formData.port,
                onChange: (e) => handlePortChange(e.target.value),
                className: errors.port ? "border-destructive" : ""
              }
            ),
            /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs text-muted-foreground", children: t("proxy.portRange") })
          ] }),
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-2", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs(Label, { htmlFor: "host", className: "flex items-center gap-2", children: [
              t("proxy.bindAddress"),
              errors.host && /* @__PURE__ */ jsxRuntimeExports.jsxs("span", { className: "text-destructive text-xs flex items-center gap-1", children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx(CircleAlert, { className: "h-3 w-3" }),
                errors.host
              ] }),
              !errors.host && formData.host && /* @__PURE__ */ jsxRuntimeExports.jsx(CircleCheck, { className: "h-3 w-3 text-green-500" })
            ] }),
            /* @__PURE__ */ jsxRuntimeExports.jsx(
              Input,
              {
                id: "host",
                placeholder: "127.0.0.1",
                value: formData.host,
                onChange: (e) => handleHostChange(e.target.value),
                className: errors.host ? "border-destructive" : ""
              }
            ),
            /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs text-muted-foreground", children: t("proxy.bindAddressHelp") })
          ] })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-4 pt-4 border-t", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-0.5", children: [
              /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { htmlFor: "cors-toggle", children: t("proxy.enableCors") }),
              /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-sm text-muted-foreground", children: t("proxy.enableCorsHelp") })
            ] }),
            /* @__PURE__ */ jsxRuntimeExports.jsx(
              Switch,
              {
                id: "cors-toggle",
                checked: formData.enableCors,
                onCheckedChange: handleCorsToggle
              }
            )
          ] }),
          formData.enableCors && /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-2", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs(Label, { htmlFor: "cors-origin", className: "flex items-center gap-2", children: [
              t("proxy.corsOrigin"),
              errors.corsOrigin && /* @__PURE__ */ jsxRuntimeExports.jsxs("span", { className: "text-destructive text-xs flex items-center gap-1", children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx(CircleAlert, { className: "h-3 w-3" }),
                errors.corsOrigin
              ] })
            ] }),
            /* @__PURE__ */ jsxRuntimeExports.jsx(
              Input,
              {
                id: "cors-origin",
                placeholder: "* or https://example.com,https://another.com",
                value: formData.corsOrigin,
                onChange: (e) => handleCorsOriginChange(e.target.value),
                className: errors.corsOrigin ? "border-destructive" : ""
              }
            ),
            /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs text-muted-foreground", children: t("proxy.corsOriginHelp") })
          ] })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex justify-end space-x-2 pt-4", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(
            Button,
            {
              variant: "outline",
              onClick: handleReset,
              disabled: !hasChanges || isLoading,
              children: t("common.reset")
            }
          ),
          /* @__PURE__ */ jsxRuntimeExports.jsx(
            Button,
            {
              onClick: handleSave,
              disabled: !hasChanges || !isValid || isLoading,
              children: isLoading ? t("proxy.saving") : t("proxy.saveConfig")
            }
          )
        ] })
      ] })
    ] }),
    /* @__PURE__ */ jsxRuntimeExports.jsx(Dialog, { open: showRestartDialog, onOpenChange: setShowRestartDialog, children: /* @__PURE__ */ jsxRuntimeExports.jsxs(DialogContent, { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs(DialogHeader, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(DialogTitle, { children: t("proxy.restartRequired") }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(DialogDescription, { children: t("proxy.restartRequiredDesc") })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsxs(DialogFooter, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(
          Button,
          {
            variant: "outline",
            onClick: () => setShowRestartDialog(false),
            disabled: isRestarting,
            children: t("common.cancel")
          }
        ),
        /* @__PURE__ */ jsxRuntimeExports.jsx(
          Button,
          {
            onClick: handleRestartAndSave,
            disabled: isRestarting,
            children: isRestarting ? /* @__PURE__ */ jsxRuntimeExports.jsxs(jsxRuntimeExports.Fragment, { children: [
              /* @__PURE__ */ jsxRuntimeExports.jsx(RefreshCw, { className: "h-4 w-4 mr-2 animate-spin" }),
              t("proxy.restarting")
            ] }) : /* @__PURE__ */ jsxRuntimeExports.jsxs(jsxRuntimeExports.Fragment, { children: [
              /* @__PURE__ */ jsxRuntimeExports.jsx(RefreshCw, { className: "h-4 w-4 mr-2" }),
              t("proxy.restartNow")
            ] })
          }
        )
      ] })
    ] }) })
  ] });
}
function LoadBalanceConfig({ onConfigChange }) {
  const { t } = useTranslation();
  const {
    loadBalanceStrategy,
    setLoadBalanceStrategy,
    accountWeights,
    setAccountWeights,
    saveAppConfig,
    isLoading
  } = useProxyStore();
  const { toast } = useToast();
  const initialStrategyRef = reactExports.useRef(loadBalanceStrategy);
  const initialWeightsRef = reactExports.useRef(accountWeights);
  const [selectedStrategy, setSelectedStrategy] = reactExports.useState(loadBalanceStrategy);
  const [weights, setWeights] = reactExports.useState(accountWeights);
  const [accounts, setAccounts] = reactExports.useState([]);
  const [hasChanges, setHasChanges] = reactExports.useState(false);
  reactExports.useEffect(() => {
    fetchAccounts();
  }, []);
  reactExports.useEffect(() => {
    setSelectedStrategy(loadBalanceStrategy);
    setWeights(accountWeights);
    initialStrategyRef.current = loadBalanceStrategy;
    initialWeightsRef.current = accountWeights;
  }, []);
  const fetchAccounts = async () => {
    try {
      const allAccounts = await window.electronAPI.accounts.getAll();
      const providers = await window.electronAPI.providers.getAll();
      const accountsWithProvider = allAccounts.map((account) => ({
        ...account,
        provider: providers.find((p) => p.id === account.providerId)
      }));
      setAccounts(accountsWithProvider);
      const defaultWeights = accountsWithProvider.map((account) => ({
        accountId: account.id,
        weight: 100
      }));
      const mergedWeights = defaultWeights.map((dw) => {
        const existing = accountWeights.find((w) => w.accountId === dw.accountId);
        return existing || dw;
      });
      setWeights(mergedWeights);
    } catch (error) {
      console.error(t("proxy.failedToGetAccounts"), error);
    }
  };
  const handleStrategyChange = (value) => {
    setSelectedStrategy(value);
    setHasChanges(true);
    onConfigChange?.();
  };
  const handleWeightChange = (accountId, weight) => {
    setWeights((prev) => prev.map(
      (w) => w.accountId === accountId ? { ...w, weight } : w
    ));
    setHasChanges(true);
    onConfigChange?.();
  };
  const handleSave = async () => {
    setLoadBalanceStrategy(selectedStrategy);
    setAccountWeights(weights);
    const success = await saveAppConfig({
      loadBalanceStrategy: selectedStrategy
    });
    if (success) {
      setHasChanges(false);
      toast({
        title: t("common.success"),
        description: t("proxy.loadBalanceConfig")
      });
    } else {
      toast({
        title: t("common.error"),
        description: t("proxy.configSaveFailed"),
        variant: "destructive"
      });
    }
  };
  const handleReset = () => {
    setSelectedStrategy(initialStrategyRef.current);
    setWeights(initialWeightsRef.current);
    setHasChanges(false);
  };
  const activeAccounts = accounts.filter((a) => a.status === "active");
  const getStrategyLabel = (strategy) => {
    const labels = {
      "round-robin": t("proxy.roundRobin"),
      "fill-first": t("proxy.fillFirst"),
      "failover": t("proxy.failover")
    };
    return labels[strategy];
  };
  const getStrategyDescription = (strategy) => {
    const descriptions = {
      "round-robin": t("proxy.roundRobinDesc"),
      "fill-first": t("proxy.fillFirstDesc"),
      "failover": t("proxy.failoverDesc")
    };
    return descriptions[strategy];
  };
  return /* @__PURE__ */ jsxRuntimeExports.jsxs(Card, { children: [
    /* @__PURE__ */ jsxRuntimeExports.jsxs(CardHeader, { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Scale, { className: "h-5 w-5 text-primary" }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(CardTitle, { children: t("proxy.loadBalanceConfig") })
        ] }),
        hasChanges && /* @__PURE__ */ jsxRuntimeExports.jsx(Badge, { variant: "secondary", className: "text-xs", children: t("proxy.unsaved") })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsx(CardDescription, { children: t("proxy.loadBalanceConfigDesc") })
    ] }),
    /* @__PURE__ */ jsxRuntimeExports.jsxs(CardContent, { className: "space-y-6", children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-2", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { htmlFor: "strategy", children: t("proxy.loadBalanceStrategy") }),
        /* @__PURE__ */ jsxRuntimeExports.jsxs(Select, { value: selectedStrategy, onValueChange: handleStrategyChange, children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(SelectTrigger, { id: "strategy", children: /* @__PURE__ */ jsxRuntimeExports.jsx(SelectValue, { placeholder: t("proxy.selectStrategy") }) }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(SelectContent, { children: ["round-robin", "fill-first", "failover"].map((strategy) => /* @__PURE__ */ jsxRuntimeExports.jsx(SelectItem, { value: strategy, children: getStrategyLabel(strategy) }, strategy)) })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-start gap-2 p-3 bg-muted/50 rounded-lg", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Info, { className: "h-4 w-4 text-muted-foreground mt-0.5 flex-shrink-0" }),
          /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-sm text-muted-foreground", children: getStrategyDescription(selectedStrategy) })
        ] })
      ] }),
      activeAccounts.length > 0 && /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-4 pt-4 border-t", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { children: t("proxy.accountWeightConfig") }),
          /* @__PURE__ */ jsxRuntimeExports.jsxs(
            Button,
            {
              variant: "ghost",
              size: "sm",
              onClick: fetchAccounts,
              className: "h-8 px-2",
              children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx(RefreshCw, { className: "h-3.5 w-3.5 mr-1" }),
                t("common.refresh")
              ]
            }
          )
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "space-y-4", children: activeAccounts.map((account) => {
          const weight = weights.find((w) => w.accountId === account.id)?.weight || 100;
          return /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-2", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between", children: [
              /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2", children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx("span", { className: "text-sm font-medium", children: account.name }),
                account.provider && /* @__PURE__ */ jsxRuntimeExports.jsx(Badge, { variant: "outline", className: "text-xs", children: account.provider.name })
              ] }),
              /* @__PURE__ */ jsxRuntimeExports.jsxs("span", { className: "text-sm text-muted-foreground w-12 text-right", children: [
                weight,
                "%"
              ] })
            ] }),
            /* @__PURE__ */ jsxRuntimeExports.jsx(
              Slider,
              {
                value: [weight],
                onValueChange: ([value]) => handleWeightChange(account.id, value),
                max: 100,
                step: 5,
                className: "w-full"
              }
            )
          ] }, account.id);
        }) }),
        /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs text-muted-foreground", children: t("proxy.weightHelp") })
      ] }),
      activeAccounts.length === 0 && /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "text-center py-6 text-muted-foreground", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-sm", children: t("proxy.noActiveAccounts") }),
        /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs mt-1", children: t("proxy.addAndActivateAccount") })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex justify-end space-x-2 pt-4", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(
          Button,
          {
            variant: "outline",
            onClick: handleReset,
            disabled: !hasChanges || isLoading,
            children: t("common.reset")
          }
        ),
        /* @__PURE__ */ jsxRuntimeExports.jsx(
          Button,
          {
            onClick: handleSave,
            disabled: !hasChanges || isLoading,
            children: isLoading ? t("proxy.saving") : t("proxy.saveConfig")
          }
        )
      ] })
    ] })
  ] });
}
function ProxyStatus({ onStatusChange }) {
  const { t } = useTranslation();
  const {
    proxyStatus,
    proxyStatistics,
    fetchProxyStatus,
    fetchProxyStatistics,
    startProxy,
    stopProxy,
    isLoading
  } = useProxyStore();
  const { toast } = useToast();
  const [isRefreshing, setIsRefreshing] = reactExports.useState(false);
  reactExports.useEffect(() => {
    fetchProxyStatus();
    fetchProxyStatistics();
    const statusInterval = setInterval(() => {
      fetchProxyStatus();
      fetchProxyStatistics();
    }, 5e3);
    return () => {
      clearInterval(statusInterval);
    };
  }, [fetchProxyStatus, fetchProxyStatistics]);
  const handleStart = async () => {
    const success = await startProxy();
    if (success) {
      toast({
        title: t("common.success"),
        description: t("dashboard.proxyRunning")
      });
      onStatusChange?.();
    } else {
      toast({
        title: t("common.error"),
        description: t("dashboard.proxyStopped"),
        variant: "destructive"
      });
    }
  };
  const handleStop = async () => {
    const success = await stopProxy();
    if (success) {
      toast({
        title: t("common.success"),
        description: t("dashboard.proxyStopped")
      });
      onStatusChange?.();
    } else {
      toast({
        title: t("common.error"),
        description: "Unable to stop proxy service",
        variant: "destructive"
      });
    }
  };
  const handleRefresh = async () => {
    setIsRefreshing(true);
    await Promise.all([fetchProxyStatus(), fetchProxyStatistics()]);
    setIsRefreshing(false);
  };
  const formatUptime = (ms) => {
    const seconds = Math.floor(ms / 1e3);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);
    if (days > 0) return `${days}d ${hours % 24}h`;
    if (hours > 0) return `${hours}h ${minutes % 60}m`;
    if (minutes > 0) return `${minutes}m ${seconds % 60}s`;
    return `${seconds}s`;
  };
  const formatLatency = (ms) => {
    if (ms < 1e3) return `${Math.round(ms)}ms`;
    return `${(ms / 1e3).toFixed(2)}s`;
  };
  const getSuccessRate = () => {
    const total = proxyStatistics?.totalRequests ?? 0;
    const success = proxyStatistics?.successRequests ?? 0;
    if (total === 0) return 0;
    return Math.round(success / total * 100);
  };
  const isRunning = proxyStatus?.isRunning ?? false;
  const statsCards = [
    {
      title: t("dashboard.totalRequests"),
      value: proxyStatistics?.totalRequests ?? 0,
      icon: TrendingUp,
      color: "text-primary"
    },
    {
      title: t("common.success"),
      value: proxyStatistics?.successRequests ?? 0,
      icon: CircleCheck,
      color: "text-green-500"
    },
    {
      title: t("common.error"),
      value: proxyStatistics?.failedRequests ?? 0,
      icon: CircleX,
      color: "text-destructive"
    },
    {
      title: t("dashboard.successRate"),
      value: `${getSuccessRate()}%`,
      icon: Activity,
      color: "text-blue-500"
    },
    {
      title: t("dashboard.avgLatency"),
      value: formatLatency(proxyStatistics?.avgLatency ?? 0),
      icon: Timer,
      color: "text-amber-500"
    }
  ];
  return /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-6", children: [
    /* @__PURE__ */ jsxRuntimeExports.jsxs(Card, { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs(CardHeader, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsx(Activity, { className: "h-5 w-5 text-primary" }),
            /* @__PURE__ */ jsxRuntimeExports.jsx(CardTitle, { children: t("dashboard.proxyStatus") })
          ] }),
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsx(
              Badge,
              {
                variant: isRunning ? "default" : "secondary",
                className: isRunning ? "bg-green-500 hover:bg-green-600" : "",
                children: isRunning ? /* @__PURE__ */ jsxRuntimeExports.jsxs(jsxRuntimeExports.Fragment, { children: [
                  /* @__PURE__ */ jsxRuntimeExports.jsx(CircleCheck, { className: "h-3 w-3 mr-1" }),
                  t("dashboard.running")
                ] }) : /* @__PURE__ */ jsxRuntimeExports.jsxs(jsxRuntimeExports.Fragment, { children: [
                  /* @__PURE__ */ jsxRuntimeExports.jsx(Square, { className: "h-3 w-3 mr-1" }),
                  t("dashboard.stopped")
                ] })
              }
            ),
            /* @__PURE__ */ jsxRuntimeExports.jsx(
              Button,
              {
                variant: "ghost",
                size: "icon",
                onClick: handleRefresh,
                disabled: isRefreshing,
                children: /* @__PURE__ */ jsxRuntimeExports.jsx(RefreshCw, { className: `h-4 w-4 ${isRefreshing ? "animate-spin" : ""}` })
              }
            )
          ] })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(CardDescription, { children: t("proxy.description") })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsxs(CardContent, { className: "space-y-6", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "grid gap-4 md:grid-cols-2", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-1", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-sm text-muted-foreground", children: t("dashboard.port") }),
            /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-2xl font-bold", children: proxyStatus?.port ?? "-" })
          ] }),
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-1", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-sm text-muted-foreground", children: t("dashboard.runtime") }),
            /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-2xl font-bold", children: isRunning && proxyStatus?.uptime ? formatUptime(proxyStatus.uptime) : "-" })
          ] })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "flex gap-3 pt-4", children: !isRunning ? /* @__PURE__ */ jsxRuntimeExports.jsxs(Button, { onClick: handleStart, disabled: isLoading, children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Play, { className: "h-4 w-4 mr-2" }),
          t("dashboard.startProxy")
        ] }) : /* @__PURE__ */ jsxRuntimeExports.jsxs(
          Button,
          {
            onClick: handleStop,
            disabled: isLoading,
            variant: "secondary",
            className: "bg-orange-100 text-orange-700 hover:bg-orange-200 dark:bg-orange-950/50 dark:text-orange-400 dark:hover:bg-orange-900/50",
            children: [
              /* @__PURE__ */ jsxRuntimeExports.jsx(Square, { className: "h-4 w-4 mr-2" }),
              t("dashboard.stopProxy")
            ]
          }
        ) })
      ] })
    ] }),
    /* @__PURE__ */ jsxRuntimeExports.jsxs(Card, { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs(CardHeader, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(ChartColumn, { className: "h-5 w-5 text-primary" }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(CardTitle, { children: t("logs.title") })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(CardDescription, { children: t("proxy.description") })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsxs(CardContent, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "grid gap-4 md:grid-cols-5", children: statsCards.map((stat, index) => /* @__PURE__ */ jsxRuntimeExports.jsx(
          Card,
          {
            hover: true,
            className: "bg-muted/30 border-0 shadow-none",
            children: /* @__PURE__ */ jsxRuntimeExports.jsxs(CardContent, { className: "p-4", children: [
              /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "flex items-center gap-2 mb-2", children: /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "h-8 w-8 rounded-lg bg-[var(--accent-primary)]/10 flex items-center justify-center", children: /* @__PURE__ */ jsxRuntimeExports.jsx(stat.icon, { className: cn("h-4 w-4", stat.color) }) }) }),
              /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-2xl font-bold", children: stat.value }),
              /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs text-muted-foreground mt-1", children: stat.title })
            ] })
          },
          index
        )) }),
        proxyStatistics?.modelUsage && Object.keys(proxyStatistics.modelUsage).length > 0 || proxyStatistics?.providerUsage && Object.keys(proxyStatistics.providerUsage).length > 0 ? /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "mt-6 grid gap-6 md:grid-cols-2", children: [
          proxyStatistics?.modelUsage && Object.keys(proxyStatistics.modelUsage).length > 0 && /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2 mb-3", children: [
              /* @__PURE__ */ jsxRuntimeExports.jsx(Cpu, { className: "h-4 w-4 text-primary" }),
              /* @__PURE__ */ jsxRuntimeExports.jsx("h4", { className: "text-sm font-medium", children: t("providers.modelUsage") })
            ] }),
            /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "space-y-1.5", children: Object.entries(proxyStatistics.modelUsage).sort(([, a], [, b]) => b - a).slice(0, 5).map(([model, count]) => {
              const total = Object.values(proxyStatistics.modelUsage).reduce((a, b) => a + b, 0);
              const percentage = Math.round(count / total * 100);
              return /* @__PURE__ */ jsxRuntimeExports.jsxs(
                "div",
                {
                  className: "flex items-center justify-between py-1.5 border-b border-border/50 last:border-0",
                  children: [
                    /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2 min-w-0", children: [
                      /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "w-1.5 h-1.5 rounded-full bg-primary flex-shrink-0" }),
                      /* @__PURE__ */ jsxRuntimeExports.jsx("code", { className: "text-xs truncate", title: model, children: model })
                    ] }),
                    /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2 flex-shrink-0", children: [
                      /* @__PURE__ */ jsxRuntimeExports.jsx(Badge, { variant: "secondary", className: "text-xs h-5 px-1.5", children: count }),
                      /* @__PURE__ */ jsxRuntimeExports.jsxs("span", { className: "text-xs text-muted-foreground w-8 text-right", children: [
                        percentage,
                        "%"
                      ] })
                    ] })
                  ]
                },
                model
              );
            }) })
          ] }),
          proxyStatistics?.providerUsage && Object.keys(proxyStatistics.providerUsage).length > 0 && /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2 mb-3", children: [
              /* @__PURE__ */ jsxRuntimeExports.jsx(Server, { className: "h-4 w-4 text-primary" }),
              /* @__PURE__ */ jsxRuntimeExports.jsx("h4", { className: "text-sm font-medium", children: t("providers.providerDistribution") })
            ] }),
            /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "space-y-1.5", children: Object.entries(proxyStatistics.providerUsage).sort(([, a], [, b]) => b - a).slice(0, 5).map(([providerId, count]) => {
              const total = Object.values(proxyStatistics.providerUsage).reduce((a, b) => a + b, 0);
              const percentage = Math.round(count / total * 100);
              return /* @__PURE__ */ jsxRuntimeExports.jsxs(
                "div",
                {
                  className: "flex items-center justify-between py-1.5 border-b border-border/50 last:border-0",
                  children: [
                    /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2 min-w-0", children: [
                      /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "w-1.5 h-1.5 rounded-full bg-primary flex-shrink-0" }),
                      /* @__PURE__ */ jsxRuntimeExports.jsx("span", { className: "text-sm truncate", title: providerId, children: providerId })
                    ] }),
                    /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2 flex-shrink-0", children: [
                      /* @__PURE__ */ jsxRuntimeExports.jsx(Badge, { variant: "secondary", className: "text-xs h-5 px-1.5", children: count }),
                      /* @__PURE__ */ jsxRuntimeExports.jsxs("span", { className: "text-xs text-muted-foreground w-8 text-right", children: [
                        percentage,
                        "%"
                      ] })
                    ] })
                  ]
                },
                providerId
              );
            }) })
          ] })
        ] }) : null
      ] })
    ] })
  ] });
}
function AdvancedConfig({ onConfigChange }) {
  const { t } = useTranslation();
  const { proxyConfig, setProxyConfig, saveAppConfig, isLoading } = useProxyStore();
  const { toast } = useToast();
  const initialFormDataRef = reactExports.useRef({
    timeout: (proxyConfig.timeout / 1e3).toString(),
    retryCount: proxyConfig.retryCount.toString()
  });
  const [formData, setFormData] = reactExports.useState({
    timeout: (proxyConfig.timeout / 1e3).toString(),
    retryCount: proxyConfig.retryCount.toString()
  });
  const [errors, setErrors] = reactExports.useState({});
  const [hasChanges, setHasChanges] = reactExports.useState(false);
  reactExports.useEffect(() => {
    const newFormData = {
      timeout: (proxyConfig.timeout / 1e3).toString(),
      retryCount: proxyConfig.retryCount.toString()
    };
    setFormData(newFormData);
    initialFormDataRef.current = newFormData;
  }, []);
  const validateTimeout = (value) => {
    const timeout = parseInt(value, 10);
    if (isNaN(timeout)) return t("proxy.timeoutMustBeNumber");
    if (timeout < 1 || timeout > 300) return t("proxy.timeoutRangeError");
    return void 0;
  };
  const validateRetryCount = (value) => {
    const count = parseInt(value, 10);
    if (isNaN(count)) return t("proxy.retryCountMustBeNumber");
    if (count < 0 || count > 10) return t("proxy.retryCountRangeError");
    return void 0;
  };
  const handleTimeoutChange = (value) => {
    setFormData((prev) => ({ ...prev, timeout: value }));
    const error = validateTimeout(value);
    setErrors((prev) => ({ ...prev, timeout: error }));
    setHasChanges(true);
    onConfigChange?.();
  };
  const handleRetryCountChange = (value) => {
    setFormData((prev) => ({ ...prev, retryCount: value }));
    const error = validateRetryCount(value);
    setErrors((prev) => ({ ...prev, retryCount: error }));
    setHasChanges(true);
    onConfigChange?.();
  };
  const handleSave = async () => {
    const timeoutError = validateTimeout(formData.timeout);
    const retryError = validateRetryCount(formData.retryCount);
    if (timeoutError || retryError) {
      setErrors({
        timeout: timeoutError,
        retryCount: retryError
      });
      toast({
        title: t("proxy.validationFailed"),
        description: t("proxy.fixFormErrors"),
        variant: "destructive"
      });
      return;
    }
    const newProxyConfig = {
      timeout: parseInt(formData.timeout, 10) * 1e3,
      retryCount: parseInt(formData.retryCount, 10)
    };
    setProxyConfig(newProxyConfig);
    const success = await saveAppConfig({
      requestTimeout: newProxyConfig.timeout,
      retryCount: newProxyConfig.retryCount
    });
    if (success) {
      setHasChanges(false);
      toast({
        title: t("common.success"),
        description: t("proxy.advancedConfigUpdated")
      });
    } else {
      toast({
        title: t("common.error"),
        description: t("proxy.configSaveFailed"),
        variant: "destructive"
      });
    }
  };
  const handleReset = () => {
    setFormData(initialFormDataRef.current);
    setErrors({});
    setHasChanges(false);
  };
  const isValid = !errors.timeout && !errors.retryCount;
  return /* @__PURE__ */ jsxRuntimeExports.jsxs(Card, { children: [
    /* @__PURE__ */ jsxRuntimeExports.jsxs(CardHeader, { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Settings2, { className: "h-5 w-5 text-primary" }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(CardTitle, { children: t("proxy.advancedConfig") })
        ] }),
        hasChanges && /* @__PURE__ */ jsxRuntimeExports.jsx(Badge, { variant: "secondary", className: "text-xs", children: t("proxy.unsaved") })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsx(CardDescription, { children: t("proxy.advancedConfigDesc") })
    ] }),
    /* @__PURE__ */ jsxRuntimeExports.jsxs(CardContent, { className: "space-y-6", children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-4", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx("h4", { className: "text-sm font-medium", children: t("proxy.requestConfig") }),
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "grid gap-4 md:grid-cols-2", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-2", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs(Label, { htmlFor: "timeout", className: "flex items-center gap-2", children: [
              t("proxy.requestTimeout"),
              errors.timeout && /* @__PURE__ */ jsxRuntimeExports.jsxs("span", { className: "text-destructive text-xs flex items-center gap-1", children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx(CircleAlert, { className: "h-3 w-3" }),
                errors.timeout
              ] }),
              !errors.timeout && formData.timeout && /* @__PURE__ */ jsxRuntimeExports.jsx(CircleCheck, { className: "h-3 w-3 text-green-500" })
            ] }),
            /* @__PURE__ */ jsxRuntimeExports.jsx(
              Input,
              {
                id: "timeout",
                type: "number",
                placeholder: "60",
                value: formData.timeout,
                onChange: (e) => handleTimeoutChange(e.target.value),
                className: errors.timeout ? "border-destructive" : ""
              }
            ),
            /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs text-muted-foreground", children: t("proxy.timeoutHelp") })
          ] }),
          /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-2", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsxs(Label, { htmlFor: "retryCount", className: "flex items-center gap-2", children: [
              t("proxy.retryCount"),
              errors.retryCount && /* @__PURE__ */ jsxRuntimeExports.jsxs("span", { className: "text-destructive text-xs flex items-center gap-1", children: [
                /* @__PURE__ */ jsxRuntimeExports.jsx(CircleAlert, { className: "h-3 w-3" }),
                errors.retryCount
              ] }),
              !errors.retryCount && formData.retryCount && /* @__PURE__ */ jsxRuntimeExports.jsx(CircleCheck, { className: "h-3 w-3 text-green-500" })
            ] }),
            /* @__PURE__ */ jsxRuntimeExports.jsx(
              Input,
              {
                id: "retryCount",
                type: "number",
                placeholder: "3",
                value: formData.retryCount,
                onChange: (e) => handleRetryCountChange(e.target.value),
                className: errors.retryCount ? "border-destructive" : ""
              }
            ),
            /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-xs text-muted-foreground", children: t("proxy.retryCountHelp") })
          ] })
        ] })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex justify-end space-x-2 pt-4", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(
          Button,
          {
            variant: "outline",
            onClick: handleReset,
            disabled: !hasChanges || isLoading,
            children: t("common.reset")
          }
        ),
        /* @__PURE__ */ jsxRuntimeExports.jsx(
          Button,
          {
            onClick: handleSave,
            disabled: !hasChanges || !isValid || isLoading,
            children: isLoading ? t("proxy.saving") : t("proxy.saveConfig")
          }
        )
      ] })
    ] })
  ] });
}
function ProxySettings() {
  const { t } = useTranslation();
  const { fetchAppConfig, fetchProxyStatus, fetchProxyStatistics } = useProxyStore();
  const hasLoadedRef = reactExports.useRef(false);
  reactExports.useEffect(() => {
    if (hasLoadedRef.current) return;
    hasLoadedRef.current = true;
    fetchAppConfig();
    fetchProxyStatus();
    fetchProxyStatistics();
  }, []);
  return /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-6", children: [
    /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsx("h2", { className: "text-2xl font-bold tracking-tight", children: t("proxy.title") }),
      /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-muted-foreground", children: t("proxy.description") })
    ] }),
    /* @__PURE__ */ jsxRuntimeExports.jsxs(Tabs, { defaultValue: "status", className: "w-full", children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs(TabsList, { className: "flex flex-wrap w-full gap-1 h-auto p-1", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs(TabsTrigger, { value: "status", className: "flex items-center gap-2 py-2 px-3 flex-1 min-w-0", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Activity, { className: "h-4 w-4 flex-shrink-0" }),
          /* @__PURE__ */ jsxRuntimeExports.jsx("span", { className: "hidden md:inline truncate", children: t("proxy.statusMonitoring") })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsxs(TabsTrigger, { value: "basic", className: "flex items-center gap-2 py-2 px-3 flex-1 min-w-0", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Settings, { className: "h-4 w-4 flex-shrink-0" }),
          /* @__PURE__ */ jsxRuntimeExports.jsx("span", { className: "hidden md:inline truncate", children: t("proxy.basicConfig") })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsxs(TabsTrigger, { value: "loadbalance", className: "flex items-center gap-2 py-2 px-3 flex-1 min-w-0", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Scale, { className: "h-4 w-4 flex-shrink-0" }),
          /* @__PURE__ */ jsxRuntimeExports.jsx("span", { className: "hidden md:inline truncate", children: t("proxy.loadBalancing") })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsxs(TabsTrigger, { value: "advanced", className: "flex items-center gap-2 py-2 px-3 flex-1 min-w-0", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Settings2, { className: "h-4 w-4 flex-shrink-0" }),
          /* @__PURE__ */ jsxRuntimeExports.jsx("span", { className: "hidden md:inline truncate", children: t("proxy.advancedConfig") })
        ] })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsx(TabsContent, { value: "status", className: "mt-6", children: /* @__PURE__ */ jsxRuntimeExports.jsx(ProxyStatus, {}) }),
      /* @__PURE__ */ jsxRuntimeExports.jsx(TabsContent, { value: "basic", className: "mt-6", children: /* @__PURE__ */ jsxRuntimeExports.jsx(ProxyConfigForm, {}) }),
      /* @__PURE__ */ jsxRuntimeExports.jsx(TabsContent, { value: "loadbalance", className: "mt-6", children: /* @__PURE__ */ jsxRuntimeExports.jsx(LoadBalanceConfig, {}) }),
      /* @__PURE__ */ jsxRuntimeExports.jsx(TabsContent, { value: "advanced", className: "mt-6", children: /* @__PURE__ */ jsxRuntimeExports.jsx(AdvancedConfig, {}) })
    ] })
  ] });
}
export {
  ProxySettings,
  ProxySettings as default
};
