import assert from "node:assert/strict";
import { test } from "node:test";
import { externalEntityApi } from "../../src/services/das/external-entities";
import { resolvePdfParties } from "../../src/services/das/pdf-party-resolver";
const signal = () => new AbortController().signal;
test("sender resolves only from a complete unique active partner response", async () => {
  const before = externalEntityApi.getList;
  try {
    externalEntityApi.getList = async (query, s) => {
      assert.equal(query?.isActive, true);
      assert.equal(query?.searchTerm, undefined);
      assert.ok(s);
      return {
        items: [{ id: "partner", fullName: "Công ty ABC", shortName: null }],
        totalCount: 1,
      } as any;
    };
    const result = await resolvePdfParties(
      { senderName: "CONG TY ABC", recipientNames: ["IT"] },
      [{ id: "target", name: "IT" }],
      signal(),
    );
    assert.equal(result.sender?.option?.id, "partner");
    assert.equal(result.recipients[0].option?.id, "target");
  } finally {
    externalEntityApi.getList = before;
  }
});
test("ambiguous sender and ambiguous receiving unit require manual selection", async () => {
  const before = externalEntityApi.getList;
  try {
    externalEntityApi.getList = async () =>
      ({
        items: [
          { id: "one", fullName: "ABC" },
          { id: "two", fullName: "ABC" },
        ],
        totalCount: 2,
      }) as any;
    const result = await resolvePdfParties(
      { senderName: "ABC", recipientNames: ["IT"] },
      [
        { id: "a", name: "IT" },
        { id: "b", name: "IT" },
      ],
      signal(),
    );
    assert.equal(result.sender?.status, "ambiguous");
    assert.equal(result.sender?.option, undefined);
    assert.equal(result.recipients[0].status, "ambiguous");
  } finally {
    externalEntityApi.getList = before;
  }
});
test("partner failure keeps OCR suggestions usable and requires manual selection", async () => {
  const before = externalEntityApi.getList;
  try {
    externalEntityApi.getList = async () => {
      throw new Error("Unavailable");
    };
    assert.equal(
      (await resolvePdfParties({ senderName: "ABC" }, [], signal())).sender
        ?.status,
      "unavailable",
    );
  } finally {
    externalEntityApi.getList = before;
  }
});
test("no sender suggestion makes no partner API request", async () => {
  const before = externalEntityApi.getList;
  try {
    externalEntityApi.getList = async () => {
      assert.fail("Unexpected lookup");
    };
    const result = await resolvePdfParties({}, [], signal());
    assert.equal(result.sender, undefined);
    assert.deepEqual(result.recipients, []);
  } finally {
    externalEntityApi.getList = before;
  }
});
test("cancelled lookup never returns a match into another form", async () => {
  const before = externalEntityApi.getList,
    controller = new AbortController();
  try {
    externalEntityApi.getList = async () => {
      controller.abort();
      return { items: [], totalCount: 0 } as any;
    };
    await assert.rejects(
      resolvePdfParties({ senderName: "ABC" }, [], controller.signal),
    );
  } finally {
    externalEntityApi.getList = before;
  }
});
test("accent-sensitive server search falls back to a complete active catalog without inventing a match", async () => {
  const before = externalEntityApi.getList;
  let calls = 0;
  try {
    externalEntityApi.getList = async (query) => {
      calls++;
      return (
        query?.searchTerm
          ? { items: [], totalCount: 0 }
          : {
              items: [
                {
                  id: "one",
                  fullName: "Văn phòng Chính phủ",
                  shortName: "VPCP",
                },
              ],
              totalCount: 1,
            }
      ) as any;
    };
    const result = await resolvePdfParties(
      { senderName: "VAN PHONG CHINH PHU" },
      [],
      signal(),
    );
    assert.equal(result.sender?.option?.id, "one");
    assert.equal(calls, 1);
  } finally {
    externalEntityApi.getList = before;
  }
});
test("stalled partner lookup times out to manual selection instead of leaving OCR busy", async () => {
  const before = externalEntityApi.getList;
  try {
    externalEntityApi.getList = async (_query, s) =>
      new Promise((_resolve, reject) =>
        s!.addEventListener("abort", () => reject(new Error("Timed out")), {
          once: true,
        }),
      );
    assert.equal(
      (await resolvePdfParties({ senderName: "ABC" }, [], signal(), 10)).sender
        ?.status,
      "unavailable",
    );
  } finally {
    externalEntityApi.getList = before;
  }
});
test("filtered search cannot hide another normalized sender match", async () => {
  const before = externalEntityApi.getList;
  try {
    externalEntityApi.getList = async (query) =>
      ({
        items: query?.searchTerm
          ? [{ id: "one", fullName: "Công ty ABC" }]
          : [
              { id: "one", fullName: "Công ty ABC" },
              { id: "two", fullName: "Cong ty ABC" },
            ],
        totalCount: query?.searchTerm ? 1 : 2,
      }) as any;
    const result = await resolvePdfParties(
      { senderName: "Công ty ABC" },
      [],
      signal(),
    );
    assert.equal(result.sender?.status, "ambiguous");
    assert.equal(result.sender?.option, undefined);
  } finally {
    externalEntityApi.getList = before;
  }
});
test("catalog larger than the bounded lookup requires manual selection", async () => {
  const before = externalEntityApi.getList;
  try {
    externalEntityApi.getList = async () =>
      ({ items: [{ id: "one", fullName: "ABC" }], totalCount: 401 }) as any;
    const result = await resolvePdfParties({ senderName: "ABC" }, [], signal());
    assert.equal(result.sender?.status, "incomplete");
    assert.equal(result.sender?.option, undefined);
  } finally {
    externalEntityApi.getList = before;
  }
});
test("sender uniqueness is checked across all bounded catalog pages", async () => {
  const before = externalEntityApi.getList;
  try {
    externalEntityApi.getList = async (query) =>
      ({
        items:
          query?.pageNumber === 1
            ? Array.from({ length: 100 }, (_, i) => ({
                id: "id-" + i,
                fullName: i === 0 ? "Công ty ABC" : "Unit " + i,
              }))
            : [{ id: "id-100", fullName: "Cong ty ABC" }],
        totalCount: 101,
      }) as any;
    assert.equal(
      (await resolvePdfParties({ senderName: "Công ty ABC" }, [], signal()))
        .sender?.status,
      "ambiguous",
    );
  } finally {
    externalEntityApi.getList = before;
  }
});
test("changing catalog totals cannot establish a unique sender", async () => {
  const before = externalEntityApi.getList;
  try {
    externalEntityApi.getList = async (query) =>
      ({
        items:
          query?.pageNumber === 1
            ? Array.from({ length: 100 }, (_, i) => ({
                id: "id-" + i,
                fullName: i === 0 ? "ABC" : "Unit " + i,
              }))
            : [],
        totalCount: query?.pageNumber === 1 ? 101 : 100,
      }) as any;
    assert.equal(
      (await resolvePdfParties({ senderName: "ABC" }, [], signal())).sender
        ?.status,
      "incomplete",
    );
  } finally {
    externalEntityApi.getList = before;
  }
});
