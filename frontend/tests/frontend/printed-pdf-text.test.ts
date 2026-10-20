import assert from "node:assert/strict";
import { test } from "node:test";
import { validatePdfText } from "../../src/services/das/printed-pdf-text";
const id = "12345678-1234-4234-8234-123456789012";
const valid = () => ({
  fileId: id,
  sha256: "a".repeat(64),
  text: "Printed report",
  pages: [{ number: 1, text: "Printed report", source: "TextLayer" }],
  suggestions: { referenceNumber: null, issuedDate: null, subject: null },
  requiresReview: true,
  warnings: [],
});
test("text layer response keeps text and mandatory human review", () => {
  const v = validatePdfText(valid(), id);
  assert.equal(v.text, "Printed report");
  assert.equal(v.requiresReview, true);
});
test("wrong file response is rejected before rendering another PDF text", () =>
  assert.throws(() =>
    validatePdfText(
      { ...valid(), fileId: "87654321-1234-4234-8234-123456789012" },
      id,
    ),
  ));
test("automatic registration approval from OCR is rejected", () =>
  assert.throws(() =>
    validatePdfText({ ...valid(), requiresReview: false }, id),
  ));
test("page order and unknown sources are rejected", () => {
  assert.throws(() =>
    validatePdfText(
      {
        ...valid(),
        pages: [{ number: 2, text: "Printed report", source: "TextLayer" }],
      },
      id,
    ),
  );
  assert.throws(() =>
    validatePdfText(
      {
        ...valid(),
        pages: [{ number: 1, text: "Printed report", source: "AI" }],
      },
      id,
    ),
  );
});
test("impossible date suggestion is rejected", () =>
  assert.throws(() =>
    validatePdfText(
      {
        ...valid(),
        suggestions: {
          referenceNumber: null,
          issuedDate: "2026-02-31",
          subject: null,
        },
      },
      id,
    ),
  ));
test("empty recognition retains warning without inventing extracted fields", () => {
  const v = validatePdfText(
    {
      ...valid(),
      text: "",
      pages: [{ number: 1, text: "", source: "OCR" }],
      warnings: ["NO_TEXT_RECOGNIZED"],
    },
    id,
  );
  assert.equal(v.text, "");
  assert.deepEqual(v.warnings, ["NO_TEXT_RECOGNIZED"]);
  assert.equal(v.suggestions.subject, null);
});
test("unbounded text and unsafe file ID are rejected", () => {
  assert.throws(() =>
    validatePdfText({ ...valid(), text: "X".repeat(1_000_001) }, id),
  );
  assert.throws(() => validatePdfText(valid(), "../private"));
});
test('sender/recipient suggestions are bounded names, not unvalidated payloads', () => {
  assert.throws(()=>validatePdfText({...valid(),suggestions:{...valid().suggestions,senderName:'x'.repeat(501)}},id))
  assert.throws(()=>validatePdfText({...valid(),suggestions:{...valid().suggestions,recipientNames:Array(21).fill('IT')}},id))
  assert.throws(()=>validatePdfText({...valid(),suggestions:{...valid().suggestions,recipientNames:['']}},id))
  assert.equal(validatePdfText({...valid(),suggestions:{...valid().suggestions,senderName:'ABC',recipientNames:['IT']}},id).suggestions.senderName,'ABC')
})
