import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { relative, resolve } from 'node:path'
import { test } from 'node:test'

import ts from 'typescript'

const root = process.cwd()
const config = ts.readConfigFile(resolve(root, 'tsconfig.json'), ts.sys.readFile)

assert.equal(config.error, undefined)
const compiler = ts.convertCompilerOptionsFromJson(config.config.compilerOptions, root)

assert.deepEqual(compiler.errors, [])

// Follow real local runtime imports, excluding type-only edges and vendor packages.
// Dynamic imports with literal paths are included; computed paths are not proved by this check.
const reachable = (entry: string | string[]) => {
  const seen = new Set<string>()
  const pending = (Array.isArray(entry) ? entry : [entry]).map(path => resolve(root, path))

  while (pending.length) {
    const path = pending.pop()!

    if (seen.has(path)) continue
    seen.add(path)
    const source = ts.createSourceFile(path, readFileSync(path, 'utf8'), ts.ScriptTarget.Latest, true)
    const imports: string[] = []
    const visit = (node: ts.Node) => {
      if (ts.isImportDeclaration(node) && ts.isStringLiteral(node.moduleSpecifier)) {
        const clause = node.importClause
        const typeOnly = clause?.isTypeOnly || (!clause?.name && clause?.namedBindings &&
          ts.isNamedImports(clause.namedBindings) && clause.namedBindings.elements.length > 0 &&
          clause.namedBindings.elements.every(element => element.isTypeOnly))

        if (!typeOnly) imports.push(node.moduleSpecifier.text)
      } else if (ts.isExportDeclaration(node) && !node.isTypeOnly && node.moduleSpecifier && ts.isStringLiteral(node.moduleSpecifier)) {
        imports.push(node.moduleSpecifier.text)
      } else if (ts.isCallExpression(node) && node.expression.kind === ts.SyntaxKind.ImportKeyword &&
        node.arguments.length === 1 && ts.isStringLiteral(node.arguments[0])) {
        imports.push(node.arguments[0].text)
      }
      ts.forEachChild(node, visit)
    }

    visit(source)
    for (const name of imports) {
      const target = ts.resolveModuleName(name, path, compiler.options, ts.sys).resolvedModule?.resolvedFileName

      if (target && relative(root, target).replaceAll('\\', '/').startsWith('src/')) pending.push(target)
    }
  }

  return [...seen].map(path => relative(root, path).replaceAll('\\', '/'))
}

test('Shared DAS provider runtime does not preload template fake-db records', () => {
  assert.deepEqual(reachable('src/components/Providers.tsx').filter(path => path.startsWith('src/fake-db/')), [])
})

test('Shared DAS provider runtime cannot schedule the deferred legacy browser email scanner', () => {
  assert.equal(reachable('src/components/Providers.tsx').includes('src/components/AutoEmailScanner.tsx'), false)
})

test('App Router page/layout literal ES-module runtime graphs exclude template fake-db records', () => {
  const entries: string[] = []
  const visitDirectory = (path: string) => {
    for (const entry of readdirSync(path, { withFileTypes: true })) {
      const child = resolve(path, entry.name)

      if (entry.isDirectory()) visitDirectory(child)
      else if (/^(page|layout)\.(tsx?|jsx?)$/.test(entry.name)) entries.push(child)
    }
  }

  visitDirectory(resolve(root, 'src/app'))
  assert.ok(entries.length > 0)
  assert.deepEqual(reachable(entries).filter(path => path.startsWith('src/fake-db/')), [])
})
