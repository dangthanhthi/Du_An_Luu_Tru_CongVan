// The single canonical schema lives in database/. Stage only its build path metadata
// below the frontend so Prisma 6 resolves the frontend's locked dependencies.
const fs = require('node:fs')
const path = require('node:path')
const { spawnSync } = require('node:child_process')

const frontend = path.resolve(__dirname, '..')
const canonical = path.resolve(frontend, '../database/prisma/schema.prisma')
const artifacts = path.join(frontend, '.artifacts')
fs.mkdirSync(artifacts, { recursive: true })
if (fs.lstatSync(artifacts).isSymbolicLink() || fs.realpathSync(artifacts) !== artifacts) {
  throw new Error('Prisma staging must stay inside the frontend artifact directory')
}
const original = fs.readFileSync(canonical, 'utf8')
const output = '  output          = "../../frontend/node_modules/.prisma/client"'
if (original.split(output).length !== 2) throw new Error('Unexpected canonical Prisma output path')
const stage = fs.mkdtempSync(path.join(artifacts, 'prisma-'))
try {
  const schema = path.join(stage, 'schema.prisma')
  fs.writeFileSync(schema, original.replace(output, '  output          = "../../node_modules/.prisma/client"'))
  const result = spawnSync(process.execPath, [require.resolve('prisma/build/index.js'), 'generate', '--schema', schema], {
    cwd: frontend,
    stdio: 'inherit',
    env: { ...process.env, PRISMA_GENERATE_SKIP_AUTOINSTALL: '1' }
  })
  if (result.error) throw result.error
  if (fs.readFileSync(canonical, 'utf8') !== original) throw new Error('Canonical Prisma schema changed during generation')
  process.exitCode = result.status === 0 ? 0 : 1
} finally {
  // mkdtemp created this directory; it contains only the staged build schema.
  fs.rmSync(stage, { recursive: true, force: true })
}
