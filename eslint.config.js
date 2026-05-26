import js from "@eslint/js";

export default [
  {
    ignores: ["Assets/dsd-api/**", "node_modules/**"],
  },
  js.configs.recommended,
  {
    files: ["Assets/agent/**/*.js", "Assets/dsd-api-ui/**/*.js"],
    languageOptions: {
      ecmaVersion: 2022,
      sourceType: "script",
      globals: {
        window: "readonly",
        document: "readonly",
        chrome: "readonly",
        localStorage: "readonly",
        console: "readonly",
        setTimeout: "readonly",
        clearTimeout: "readonly",
        setInterval: "readonly",
        clearInterval: "readonly",
        fetch: "readonly",
        DsAgentEmbed: "readonly",
        DsWorkMode: "readonly",
      },
    },
    rules: {
      "no-unused-vars": ["warn", { argsIgnorePattern: "^_" }],
    },
  },
];
