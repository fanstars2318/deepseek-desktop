import { u as useTranslation, k as useSettingsStore, r as reactExports, j as jsxRuntimeExports, a5 as Key, B as Button, L as Copy, M as Dialog, N as DialogContent, O as DialogHeader, Q as DialogTitle, T as DialogDescription, U as DialogFooter, a6 as toast } from "./index-CBGGrI33.js";
import { C as Card, a as CardHeader, b as CardTitle, d as CardDescription, c as CardContent } from "./card-B5CbN4-5.js";
import { L as Label, S as Switch, I as Input } from "./label-mNw1xWB1.js";
import { T as Table, a as TableHeader, b as TableRow, c as TableHead, d as TableBody, e as TableCell } from "./table-CycXrrfD.js";
import { S as Shield } from "./shield-Dkbamauq.js";
import { P as Plus } from "./plus-D8MLKvJm.js";
import { E as EyeOff, a as Eye } from "./eye-B4JnEcH-.js";
import { C as ChartColumn } from "./chart-column-BBsiuvoH.js";
import { C as Clock } from "./clock-Cy3AVtaW.js";
import { T as Trash2 } from "./trash-2-Csxc6eE1.js";
import "./index-DOD0wY2v.js";
function generateApiKey() {
  const chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
  let key = "sk-";
  for (let i = 0; i < 48; i++) {
    key += chars.charAt(Math.floor(Math.random() * chars.length));
  }
  return key;
}
function generateId() {
  return Date.now().toString(36) + Math.random().toString(36).substr(2);
}
function ApiKeysPage() {
  const { t } = useTranslation();
  const { config, updateConfig, fetchConfig } = useSettingsStore();
  const [showAddDialog, setShowAddDialog] = reactExports.useState(false);
  const [newKeyName, setNewKeyName] = reactExports.useState("");
  const [visibleKeys, setVisibleKeys] = reactExports.useState(/* @__PURE__ */ new Set());
  const [deleteKeyId, setDeleteKeyId] = reactExports.useState(null);
  reactExports.useEffect(() => {
    fetchConfig();
  }, [fetchConfig]);
  const apiKeys = config?.apiKeys || [];
  const handleToggleEnabled = async (keyId, enabled) => {
    const updatedKeys = apiKeys.map(
      (k) => k.id === keyId ? { ...k, enabled } : k
    );
    await updateConfig({ apiKeys: updatedKeys });
    toast({
      title: enabled ? t("apiKeys.keyEnabled") : t("apiKeys.keyDisabled"),
      description: enabled ? t("apiKeys.keyEnabled") : t("apiKeys.keyDisabled")
    });
  };
  const handleAddKey = async () => {
    if (!newKeyName.trim()) {
      toast({
        title: t("apiKeys.pleaseEnterName"),
        description: t("apiKeys.keyNameRequired"),
        variant: "destructive"
      });
      return;
    }
    const newKey = {
      id: generateId(),
      name: newKeyName.trim(),
      key: generateApiKey(),
      enabled: true,
      createdAt: Date.now(),
      usageCount: 0
    };
    await updateConfig({ apiKeys: [...apiKeys, newKey] });
    setShowAddDialog(false);
    setNewKeyName("");
  };
  const handleDeleteKey = async () => {
    if (!deleteKeyId) return;
    const updatedKeys = apiKeys.filter((k) => k.id !== deleteKeyId);
    await updateConfig({ apiKeys: updatedKeys });
    setDeleteKeyId(null);
    toast({
      title: t("apiKeys.deleted"),
      description: t("apiKeys.keyDeleted")
    });
  };
  const handleCopyKey = (key) => {
    navigator.clipboard.writeText(key);
    toast({
      title: t("apiKeys.copied"),
      description: t("apiKeys.copiedToClipboard")
    });
  };
  const handleToggleVisibility = (keyId) => {
    const newVisible = new Set(visibleKeys);
    if (newVisible.has(keyId)) {
      newVisible.delete(keyId);
    } else {
      newVisible.add(keyId);
    }
    setVisibleKeys(newVisible);
  };
  const handleToggleGlobalEnabled = async (enabled) => {
    await updateConfig({ enableApiKey: enabled });
    toast({
      title: enabled ? t("apiKeys.authEnabled") : t("apiKeys.authDisabled"),
      description: enabled ? t("apiKeys.authEnabled") : t("apiKeys.authDisabled")
    });
  };
  const formatDate = (timestamp) => {
    return new Date(timestamp).toLocaleString("zh-CN", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit"
    });
  };
  const maskKey = (key) => {
    if (key.length <= 10) return key;
    return key.substring(0, 7) + "****" + key.substring(key.length - 4);
  };
  return /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-6", children: [
    /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "flex items-center justify-between", children: /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsx("h1", { className: "text-2xl font-bold", children: t("apiKeys.title") }),
      /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-muted-foreground", children: t("apiKeys.description") })
    ] }) }),
    /* @__PURE__ */ jsxRuntimeExports.jsxs(Card, { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs(CardHeader, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs(CardTitle, { className: "flex items-center gap-2", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Shield, { className: "h-5 w-5" }),
          t("apiKeys.apiKeyAuth")
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(CardDescription, { children: t("apiKeys.apiKeyAuthDesc") })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsx(CardContent, { children: /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center justify-between", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-0.5", children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { htmlFor: "global-enable", children: t("apiKeys.enableApiKeyAuth") }),
          /* @__PURE__ */ jsxRuntimeExports.jsxs("p", { className: "text-sm text-muted-foreground", children: [
            t("apiKeys.currentStatus"),
            ": ",
            config?.enableApiKey ? t("common.enabled") : t("common.disabled")
          ] })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(
          Switch,
          {
            id: "global-enable",
            checked: config?.enableApiKey || false,
            onCheckedChange: handleToggleGlobalEnabled
          }
        )
      ] }) })
    ] }),
    /* @__PURE__ */ jsxRuntimeExports.jsxs(Card, { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs(CardHeader, { className: "flex flex-row items-center justify-between", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { children: [
          /* @__PURE__ */ jsxRuntimeExports.jsxs(CardTitle, { className: "flex items-center gap-2", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsx(Key, { className: "h-5 w-5" }),
            t("apiKeys.apiKeyList")
          ] }),
          /* @__PURE__ */ jsxRuntimeExports.jsxs(CardDescription, { children: [
            t("apiKeys.totalKeys", { count: apiKeys.length }),
            ", ",
            t("apiKeys.enabledKeys", { count: apiKeys.filter((k) => k.enabled).length })
          ] })
        ] }),
        /* @__PURE__ */ jsxRuntimeExports.jsxs(Button, { onClick: () => setShowAddDialog(true), children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(Plus, { className: "h-4 w-4 mr-2" }),
          t("apiKeys.newApiKey")
        ] })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsx(CardContent, { children: apiKeys.length === 0 ? /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "text-center py-12", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(Key, { className: "h-12 w-12 mx-auto text-muted-foreground mb-4" }),
        /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-muted-foreground", children: t("apiKeys.noApiKeys") }),
        /* @__PURE__ */ jsxRuntimeExports.jsx("p", { className: "text-sm text-muted-foreground mt-1", children: t("apiKeys.clickToCreate") })
      ] }) : /* @__PURE__ */ jsxRuntimeExports.jsxs(Table, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(TableHeader, { children: /* @__PURE__ */ jsxRuntimeExports.jsxs(TableRow, { children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableHead, { children: t("apiKeys.name") }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableHead, { children: t("apiKeys.apiKey") }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableHead, { children: t("apiKeys.status") }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableHead, { children: t("apiKeys.usageCount") }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableHead, { children: t("apiKeys.createdAt") }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableHead, { children: t("apiKeys.operations") })
        ] }) }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(TableBody, { children: apiKeys.map((apiKey) => /* @__PURE__ */ jsxRuntimeExports.jsxs(TableRow, { children: [
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableCell, { children: /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "font-medium", children: apiKey.name }) }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableCell, { children: /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-2", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsx("code", { className: "text-sm bg-muted px-2 py-1 rounded", children: visibleKeys.has(apiKey.id) ? apiKey.key : maskKey(apiKey.key) }),
            /* @__PURE__ */ jsxRuntimeExports.jsx(
              Button,
              {
                variant: "ghost",
                size: "icon",
                className: "h-8 w-8",
                onClick: () => handleToggleVisibility(apiKey.id),
                children: visibleKeys.has(apiKey.id) ? /* @__PURE__ */ jsxRuntimeExports.jsx(EyeOff, { className: "h-4 w-4" }) : /* @__PURE__ */ jsxRuntimeExports.jsx(Eye, { className: "h-4 w-4" })
              }
            ),
            /* @__PURE__ */ jsxRuntimeExports.jsx(
              Button,
              {
                variant: "ghost",
                size: "icon",
                className: "h-8 w-8",
                onClick: () => handleCopyKey(apiKey.key),
                children: /* @__PURE__ */ jsxRuntimeExports.jsx(Copy, { className: "h-4 w-4" })
              }
            )
          ] }) }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableCell, { children: /* @__PURE__ */ jsxRuntimeExports.jsx(
            Switch,
            {
              checked: apiKey.enabled,
              onCheckedChange: (checked) => handleToggleEnabled(apiKey.id, checked)
            }
          ) }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableCell, { children: /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-1", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsx(ChartColumn, { className: "h-4 w-4 text-muted-foreground" }),
            apiKey.usageCount
          ] }) }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableCell, { children: /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "flex items-center gap-1 text-sm text-muted-foreground", children: [
            /* @__PURE__ */ jsxRuntimeExports.jsx(Clock, { className: "h-4 w-4" }),
            formatDate(apiKey.createdAt)
          ] }) }),
          /* @__PURE__ */ jsxRuntimeExports.jsx(TableCell, { children: /* @__PURE__ */ jsxRuntimeExports.jsx(
            Button,
            {
              variant: "ghost",
              size: "icon",
              className: "h-8 w-8 text-destructive hover:text-destructive",
              onClick: () => setDeleteKeyId(apiKey.id),
              children: /* @__PURE__ */ jsxRuntimeExports.jsx(Trash2, { className: "h-4 w-4" })
            }
          ) })
        ] }, apiKey.id)) })
      ] }) })
    ] }),
    /* @__PURE__ */ jsxRuntimeExports.jsx(Dialog, { open: showAddDialog, onOpenChange: setShowAddDialog, children: /* @__PURE__ */ jsxRuntimeExports.jsxs(DialogContent, { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs(DialogHeader, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(DialogTitle, { children: t("apiKeys.createApiKey") }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(DialogDescription, { children: t("apiKeys.createApiKeyDesc") })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsx("div", { className: "space-y-4 py-4", children: /* @__PURE__ */ jsxRuntimeExports.jsxs("div", { className: "space-y-2", children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(Label, { htmlFor: "key-name", children: t("apiKeys.keyName") }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(
          Input,
          {
            id: "key-name",
            placeholder: t("apiKeys.keyNamePlaceholder"),
            value: newKeyName,
            onChange: (e) => setNewKeyName(e.target.value)
          }
        )
      ] }) }),
      /* @__PURE__ */ jsxRuntimeExports.jsxs(DialogFooter, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(Button, { variant: "outline", onClick: () => setShowAddDialog(false), children: t("apiKeys.cancel") }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(Button, { onClick: handleAddKey, children: t("apiKeys.create") })
      ] })
    ] }) }),
    /* @__PURE__ */ jsxRuntimeExports.jsx(Dialog, { open: !!deleteKeyId, onOpenChange: () => setDeleteKeyId(null), children: /* @__PURE__ */ jsxRuntimeExports.jsxs(DialogContent, { children: [
      /* @__PURE__ */ jsxRuntimeExports.jsxs(DialogHeader, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(DialogTitle, { children: t("apiKeys.confirmDelete") }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(DialogDescription, { children: t("apiKeys.confirmDeleteDesc") })
      ] }),
      /* @__PURE__ */ jsxRuntimeExports.jsxs(DialogFooter, { children: [
        /* @__PURE__ */ jsxRuntimeExports.jsx(Button, { variant: "outline", onClick: () => setDeleteKeyId(null), children: t("apiKeys.cancel") }),
        /* @__PURE__ */ jsxRuntimeExports.jsx(Button, { variant: "destructive", onClick: handleDeleteKey, children: t("apiKeys.delete") })
      ] })
    ] }) })
  ] });
}
export {
  ApiKeysPage as default
};
