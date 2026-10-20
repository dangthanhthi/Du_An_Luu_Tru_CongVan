export type OrganizationOption = {
  id: string;
  name: string;
  shortName?: string | null;
};
export type OrganizationMatch = {
  status: "matched" | "missing" | "ambiguous" | "incomplete" | "unavailable";
  name: string;
  option?: OrganizationOption;
};
export type PdfPartyResolution = {
  sender?: OrganizationMatch;
  recipients: OrganizationMatch[];
};
export function vietnamToday(now = new Date()): string {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(now);
  const get = (key: string) => parts.find((p) => p.type === key)!.value;
  return `${get("year")}-${get("month")}-${get("day")}`;
}
function organizationKey(value: string) {
  return value
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .toLowerCase()
    .replace(/đ/g, "d")
    .replace(/[^\p{L}\p{N}]+/gu, " ")
    .trim()
    .replace(/\s+/g, " ");
}
export function matchOrganization(
  name: string,
  options: OrganizationOption[],
  complete = true,
): OrganizationMatch {
  if (!complete) return { status: "incomplete", name };
  const key = organizationKey(name);
  const matches = key
    ? options.filter(
        (o) =>
          organizationKey(o.name) === key ||
          (o.shortName && organizationKey(o.shortName) === key),
      )
    : [];
  const distinct = [...new Map(matches.map((o) => [o.id, o])).values()];
  return distinct.length === 1
    ? { status: "matched", name, option: distinct[0] }
    : { status: distinct.length > 1 ? "ambiguous" : "missing", name };
}
