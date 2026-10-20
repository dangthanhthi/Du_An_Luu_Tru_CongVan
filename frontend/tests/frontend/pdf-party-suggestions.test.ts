import assert from "node:assert/strict";
import { test } from "node:test";
import {
  matchOrganization,
  vietnamToday,
} from "../../src/services/das/pdf-party-suggestions";
test("receiving date uses Vietnam day across UTC midnight", () => {
  assert.equal(vietnamToday(new Date("2026-10-12T18:30:00Z")), "2026-10-13");
  assert.equal(vietnamToday(new Date("2026-10-12T16:59:00Z")), "2026-10-12");
});
test("a unique existing full name is matched despite OCR case/spacing/accents", () => {
  const result = matchOrganization("  VAN PHONG CHINH PHU ", [
    { id: "one", name: "Văn phòng Chính phủ" },
  ]);
  assert.equal(result.status, "matched");
  assert.equal(result.option?.id, "one");
});
test("a unique abbreviation is resolved from the existing catalog only", () => {
  assert.equal(
    matchOrganization("VPCP", [
      { id: "one", name: "Văn phòng Chính phủ", shortName: "VPCP" },
    ]).option?.id,
    "one",
  );
});
test("duplicate names require manual selection", () => {
  const result = matchOrganization("ABC", [
    { id: "one", name: "ABC" },
    { id: "two", name: "ABC" },
  ]);
  assert.equal(result.status, "ambiguous");
  assert.equal(result.option, undefined);
});
test("an unknown name is never assigned to a vaguely similar organization", () => {
  assert.equal(
    matchOrganization("ABC", [{ id: "one", name: "ABC Subsidiary" }]).status,
    "missing",
  );
});
test("an incomplete server result cannot prove a unique match", () => {
  assert.equal(
    matchOrganization("ABC", [{ id: "one", name: "ABC" }], false).status,
    "incomplete",
  );
});
