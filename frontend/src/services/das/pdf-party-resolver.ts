import { externalEntityApi } from "./external-entities";
import {
  matchOrganization,
  type OrganizationOption,
  type PdfPartyResolution,
} from "./pdf-party-suggestions";
export async function resolvePdfParties(
  suggestions: { senderName?: string | null; recipientNames?: string[] | null },
  targets: OrganizationOption[],
  signal: AbortSignal,
  timeoutMs = 8000,
): Promise<PdfPartyResolution> {
  signal.throwIfAborted();
  const recipients = [...new Set(suggestions.recipientNames ?? [])].map(
    (name) => matchOrganization(name, targets),
  );
  if (!suggestions.senderName?.trim()) return { recipients };
  const name = suggestions.senderName;
  const lookup = new AbortController(),
    cancel = () => lookup.abort();
  signal.addEventListener("abort", cancel, { once: true });
  const timer = setTimeout(cancel, Math.max(1, Math.min(timeoutMs, 8000)));
  try {
    // Raw SQL search can hide accent/punctuation variants. Only the complete
    // active catalog proves uniqueness under the normalization used here.
    const options: OrganizationOption[] = [],
      ids = new Set<string>();
    let total: number | undefined,
      complete = false;
    for (let pageNumber = 1; pageNumber <= 4; pageNumber++) {
      const page = await externalEntityApi.getList(
        { isActive: true, pageSize: 100, pageNumber },
        lookup.signal,
      );
      lookup.signal.throwIfAborted();
      if (total !== undefined && total !== page.totalCount) break;
      total = page.totalCount;
      if (total > 400) break;
      let duplicate = false;
      for (const row of page.items) {
        if (ids.has(row.id)) {
          duplicate = true;
          break;
        }
        ids.add(row.id);
        options.push({
          id: row.id,
          name: row.fullName,
          shortName: row.shortName,
        });
      }
      if (duplicate) break;
      if (options.length === total) {
        complete = true;
        break;
      }
    }
    const sender = matchOrganization(name, options, complete);
    return { sender, recipients };
  } catch {
    signal.throwIfAborted();
    return { sender: { status: "unavailable", name }, recipients };
  } finally {
    clearTimeout(timer);
    signal.removeEventListener("abort", cancel);
  }
}
