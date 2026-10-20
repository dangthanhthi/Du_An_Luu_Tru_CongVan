import {
  ApiRequestError,
  requestApiEnvelope,
  type SessionIntent,
} from "../api";
import { isDocumentGuid } from "./documents";
export type PdfTextResult = {
  fileId: string;
  sha256: string;
  text: string;
  pages: { number: number; text: string; source: "TextLayer" | "OCR" }[];
  suggestions: {
    referenceNumber: string | null;
    issuedDate: string | null;
    subject: string | null;
    senderName?: string | null;
    recipientNames?: string[] | null;
  };
  requiresReview: true;
  warnings: string[];
};
export function validatePdfText(value: unknown, fileId: string): PdfTextResult {
  const invalid = () => new ApiRequestError(502, "Phản hồi OCR không hợp lệ.");
  const x = value as PdfTextResult | null;
  const text = (v: unknown, max: number) =>
    typeof v === "string" && v.length <= max;
  const nullable = (v: unknown, max: number) => v === null || text(v, max);
  const date = (v: unknown) =>
    v === null ||
    (typeof v === "string" &&
      /^\d{4}-\d{2}-\d{2}$/.test(v) &&
      !Number.isNaN(Date.parse(v)) &&
      new Date(v).toISOString().slice(0, 10) === v);
  if (
    !isDocumentGuid(fileId) ||
    !x ||
    x.fileId !== fileId ||
    !/^[a-f0-9]{64}$/i.test(x.sha256) ||
    !text(x.text, 1_000_000) ||
    x.requiresReview !== true ||
    !Array.isArray(x.pages) ||
    x.pages.length < 1 ||
    x.pages.length > 100 ||
    x.pages.some(
      (p, i) =>
        !p ||
        p.number !== i + 1 ||
        !text(p.text, 1_000_000) ||
        !["TextLayer", "OCR"].includes(p.source),
    ) ||
    !x.suggestions ||
    !nullable(x.suggestions.referenceNumber, 200) ||
    !nullable(x.suggestions.subject, 2000) ||
    !date(x.suggestions.issuedDate) ||
    !Array.isArray(x.warnings) ||
    x.warnings.length > 100 ||
    x.warnings.some(
      (w) => typeof w !== "string" || !/^[A-Z][A-Z0-9_]{0,79}$/.test(w),
    )
  )
    throw invalid();
  if (!x.text.trim() && !x.warnings.includes("NO_TEXT_RECOGNIZED"))
    throw invalid();
  if (x.suggestions.senderName !== undefined && !nullable(x.suggestions.senderName, 500)) throw invalid();
  if (x.suggestions.recipientNames != null && (!Array.isArray(x.suggestions.recipientNames) || x.suggestions.recipientNames.length > 20 || x.suggestions.recipientNames.some(name => !text(name, 500) || !name.trim()))) throw invalid();
  return x;
}
export async function readPrintedPdf(
  fileId: string,
  sessionIntent: SessionIntent,
  signal?: AbortSignal,
) {
  if (!isDocumentGuid(fileId))
    throw new ApiRequestError(400, "Định danh PDF không hợp lệ.");
  const { data } = await requestApiEnvelope<unknown>(
    "files",
    `/api/files/${fileId}/text`,
    {
      method: "POST",
      sessionIntent,
      signal,
      cache: "no-store",
      redirect: "error",
    },
  );
  return validatePdfText(data, fileId);
}
