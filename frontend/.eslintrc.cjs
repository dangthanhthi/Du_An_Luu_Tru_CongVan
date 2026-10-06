module.exports = {
  root: true,
  env: { browser: true, node: true, es2022: true },
  parser: "@typescript-eslint/parser",
  parserOptions: { ecmaVersion: "latest", sourceType: "module", ecmaFeatures: { jsx: true } },
  extends: ["eslint:recommended"],
  plugins: ["@typescript-eslint", "react-hooks", "@next/next"],
  ignorePatterns: ["node_modules/", ".next/", ".artifacts/"],
  rules: {
    "no-empty": ["error", { allowEmptyCatch: true }],
    "no-unused-vars": "warn",
    "no-useless-escape": "warn",
    // Bounded stream readers terminate on done/size checks inside their loops.
    "no-constant-condition": ["error", { checkLoops: false }]
  },
  overrides: [{
    files: ["**/*.ts", "**/*.tsx"],
    // TypeScript checks identifiers and declaration overloads; the JS rules misread type-only names.
    rules: { "no-undef": "off", "no-unused-vars": "off", "no-dupe-class-members": "off" }
  }]
}
