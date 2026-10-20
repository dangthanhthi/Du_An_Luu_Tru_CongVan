import assert from "node:assert/strict";
import { test } from "node:test";
import {
  componentHarness,
  nodes,
  textContent,
} from "./helpers/component-harness";
import { ApiRequestError } from "../../src/services/api";
const tick = () => new Promise((resolve) => setImmediate(resolve));
const id = "12345678-1234-4234-8234-123456789012",
  other = "87654321-1234-4234-8234-123456789012";
function result() {
  return {
    fileId: id,
    sha256: "a".repeat(64),
    text: "So: 456/TEST\nPrinted report",
    pages: [{ number: 1, text: "Printed report", source: "TextLayer" }],
    suggestions: {
      referenceNumber: "456/TEST",
      issuedDate: "2026-10-13",
      subject: "Printed report",
    },
    requiresReview: true,
    warnings: [],
  };
}
function deferred() {
  let resolve!: (v: any) => void;
  let reject!: (e: any) => void;
  const promise = new Promise<any>((a, b) => {
    resolve = a;
    reject = b;
  });
  return { promise, resolve, reject };
}
function fixture(read: any) {
  let apply = 0;
  const ui = componentHarness(
    new URL(
      "../../src/views/apps/documents/PrintedPdfPanel.tsx",
      import.meta.url,
    ),
    {
      "@/services/api": { ApiRequestError },
      '@/services/das/pdf-party-resolver': { resolvePdfParties: async () => ({ recipients: [] }) },
      "@/hooks/useSessionIntent": {
        useSessionIntent: () => ({ epoch: "test", assertCurrent() {} }),
      },
      "@/hooks/useDictionary": { useAppDictionary: () => ({ isEn: true }) },
      "@/services/das/printed-pdf-text": { readPrintedPdf: read },
      "@/services/das/document-pdf": { documentPdfApi: {} },
    },
  );
  const render = (fileId = id) =>
    ui.render({
      fileId,
      onApply: () => {
        apply++;
      },
    });
  return { ui, render, applies: () => apply };
}

test('Select printed PDF opens the native input without starting OCR before selection', () => {
  let reads = 0
  const f = fixture(async () => { reads++; return result() })
  const tree = f.render('')
  const input = nodes(tree).find(n => n.type === 'input')!
  const select = nodes(tree).find(n => n.type === 'Button' && textContent(n).trim() === 'Select PDF and read')!
  assert.equal(typeof select.props.onClick, 'function', 'keyboard-activated button must open the chooser')
  let opens = 0
  input.props.ref.current = { click() { opens++ } }
  select.props.onClick()
  assert.equal(opens, 1)
  assert.equal(reads, 0)
  assert.equal(f.applies(), 0)
  f.ui.unmount()
})
test("OCR button has synchronous duplicate guard before render", async () => {
  let calls = 0;
  const d = deferred();
  const f = fixture(() => {
    calls++;
    return d.promise;
  });
  const button = nodes(f.render()).find(
    (n) => n.type === "Button" && textContent(n) === "Read printed PDF",
  )!;
  assert.ok(button);
  button.props.onClick();
  button.props.onClick();
  assert.equal(calls, 1);
  d.resolve(result());
  await tick();
  f.ui.unmount();
});
test("OCR response for previous file never appears under current file", async () => {
  const d = deferred();
  const f = fixture(() => d.promise);
  nodes(f.render())
    .find((n) => n.type === "Button" && textContent(n) === "Read printed PDF")!
    .props.onClick();
  f.render(other);
  f.ui.commit();
  d.resolve(result());
  await tick();
  assert.ok(!textContent(f.render(other)).includes("456/TEST"));
  assert.equal(f.applies(), 0);
  f.ui.unmount();
});
test("OCR suggestions require manual apply and never automatically register", async () => {
  const f = fixture(async () => result());
  nodes(f.render())
    .find((n) => n.type === "Button" && textContent(n) === "Read printed PDF")!
    .props.onClick();
  await tick();
  assert.equal(f.applies(), 0);
  const apply = nodes(f.render()).find(
    (n) => n.type === "Button" && textContent(n) === "Apply suggestions",
  )!;
  assert.ok(apply);
  apply.props.onClick();
  assert.equal(f.applies(), 1);
  f.ui.unmount();
});
test("unmounted OCR request signal is cancelled", async () => {
  const d = deferred();
  let signal: AbortSignal | undefined;
  const f = fixture((_id: any, _intent: any, s: AbortSignal) => {
    signal = s;
    return d.promise;
  });
  f.render();
  f.ui.commit();
  nodes(f.render())
    .find((n) => n.type === "Button" && textContent(n) === "Read printed PDF")!
    .props.onClick();
  f.ui.unmount();
  assert.equal(signal?.aborted, true);
  d.resolve(result());
  await tick();
});
test("returning to a previous file does not resurrect discarded recognition", async () => {
  const f = fixture(async () => result());
  f.render();
  f.ui.commit();
  nodes(f.render())
    .find((n) => n.type === "Button" && textContent(n) === "Read printed PDF")!
    .props.onClick();
  await tick();
  assert.ok(nodes(f.render()).some((n) => n.type === "TextField"));
  f.render(other);
  f.ui.commit();
  assert.ok(!nodes(f.render()).some((n) => n.type === "TextField"));
  f.ui.unmount();
});
