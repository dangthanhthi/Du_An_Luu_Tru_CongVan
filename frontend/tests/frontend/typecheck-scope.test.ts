import assert from 'node:assert/strict'
import { test } from 'node:test'
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { basename, dirname, join, resolve } from 'node:path'
import { createRequire } from 'node:module'
import { spawnSync } from 'node:child_process'

test('Real TypeScript config includes product/tests but excludes archived and QA copies', async () => {
  const require = createRequire(import.meta.url)
  const directory = await mkdtemp(join(tmpdir(), 'das-typecheck-scope-'))
  try {
    const config = JSON.parse(await readFile('tsconfig.json', 'utf8'))
    await writeFile(join(directory, 'tsconfig.json'), JSON.stringify(config))
    for (const name of ['src/app.ts', 'tests/frontend/example.test.ts', 'archive/inactive.ts', '.artifacts/qa/clean-source/src/copy.ts']) {
      const file = join(directory, name)
      await mkdir(join(file, '..'), { recursive: true })
      await writeFile(file, 'export const fixture = true')
    }
    const result = spawnSync(process.execPath, [require.resolve('typescript/lib/tsc.js'), '--showConfig', '--project', join(directory, 'tsconfig.json')], { encoding: 'utf8', timeout: 30000 })
    assert.equal(result.status, 0, result.stderr)
    const actual = JSON.parse(result.stdout)
    assert.deepEqual(actual.files.sort(), ['./src/app.ts', './tests/frontend/example.test.ts'])
    assert.equal(actual.compilerOptions.strict, true)
    assert.equal(actual.compilerOptions.noEmit, true)
  } finally {
    const target = resolve(directory)
    assert.equal(dirname(target), resolve(tmpdir()), 'Only the owned temporary fixture may be removed')
    assert.ok(basename(target).startsWith('das-typecheck-scope-'))
    await rm(target, { recursive: true, force: true })
  }
})
