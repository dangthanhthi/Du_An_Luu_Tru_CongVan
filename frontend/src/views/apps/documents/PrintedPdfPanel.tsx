"use client";
import { useEffect, useRef, useState } from "react";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import CircularProgress from "@mui/material/CircularProgress";
import { useSessionIntent } from "@/hooks/useSessionIntent";
import { useAppDictionary } from "@/hooks/useDictionary";
import {
  readPrintedPdf,
  type PdfTextResult,
} from "@/services/das/printed-pdf-text";
import { documentPdfApi } from "@/services/das/document-pdf";
import { ApiRequestError } from "@/services/api";
import { resolvePdfParties } from "@/services/das/pdf-party-resolver";
import type {
  OrganizationOption,
  PdfPartyResolution,
} from "@/services/das/pdf-party-suggestions";
type Props = {
  fileId?: string;
  disabled?: boolean;
  onApply?: (result: PdfTextResult, parties?: PdfPartyResolution) => void;
  distributionTargets?: OrganizationOption[];
  onFileReady?: (id: string | null) => void;
  onBusy?: (busy: boolean) => void;
};
function wait(signal: AbortSignal) {
  return new Promise<void>((resolve, reject) => {
    signal.throwIfAborted();
    const cancel = () => {
      clearTimeout(timer);
      reject(signal.reason);
    };
    const timer = setTimeout(() => {
      signal.removeEventListener("abort", cancel);
      resolve();
    }, 2000);
    signal.addEventListener("abort", cancel, { once: true });
  });
}
export default function PrintedPdfPanel({
  fileId,
  disabled,
  onApply,
  onFileReady,
  onBusy,
  distributionTargets,
}: Props) {
  const intent = useSessionIntent(),
    { isEn } = useAppDictionary(),
    key = `${intent.epoch}:${fileId ?? "upload"}`;
  const lifetime = useRef({ key, controller: new AbortController() });
  const callbacks = useRef({ onApply, onFileReady, onBusy });
  const fileInput = useRef<HTMLInputElement>(null);
  callbacks.current = { onApply, onFileReady, onBusy };
  const guard = useRef(false),
    keyRef = useRef(key);
  keyRef.current = key;
  if (lifetime.current.key !== key) {
    lifetime.current = { key, controller: new AbortController() };
    guard.current = false;
  }
  const owner = lifetime.current.controller;
  const [state, setState] = useState<{
    key: string;
    owner: AbortController;
    result?: PdfTextResult;
    error?: string;
    busy?: boolean;
    notice?: string;
    name?: string;
    parties?: PdfPartyResolution;
  }>({ key, owner });
  const current =
    state.key === key && state.owner === owner ? state : undefined;
  useEffect(() => {
    if (lifetime.current.controller.signal.aborted) {
      lifetime.current.controller = new AbortController();
      guard.current = false;
      setState({ key, owner: lifetime.current.controller });
    }
    const controller = lifetime.current.controller;
    return () => {
      controller.abort();
      if (lifetime.current.controller === controller)
        callbacks.current.onBusy?.(false);
    };
  }, [key]);
  const active = () =>
    !owner.signal.aborted &&
    lifetime.current.controller === owner &&
    keyRef.current === key;
  async function scan(file?: File) {
    if (disabled || guard.current || !active()) return;
    guard.current = true;
    callbacks.current.onFileReady?.(null);
    callbacks.current.onBusy?.(true);
    setState({
      key,
      owner,
      busy: true,
      notice: isEn ? "Reading printed PDF…" : "Đang đọc PDF chữ in…",
    });
    try {
      intent.assertCurrent();
      let id = fileId,
        name: string | undefined;
      if (file) {
        const uploaded = await documentPdfApi.upload(file, intent);
        id = uploaded.id;
        name = uploaded.originalName;
        for (let attempt = 0; ; attempt++) {
          owner.signal.throwIfAborted();
          intent.assertCurrent();
          const info = await documentPdfApi.uploadInfo(id, owner.signal);
          if (info.state === "Available" && info.canAttach) break;
          if (info.state !== "PendingScan" || attempt >= 60)
            throw new ApiRequestError(423, "PDF_SCAN_PENDING");
          if (active())
            setState({
              key,
              owner,
              busy: true,
              name,
              notice: isEn
                ? "PDF safety scan in progress…"
                : "PDF đang được quét an toàn…",
            });
          await wait(owner.signal);
        }
      }
      if (!id)
        throw new Error(isEn ? "Select a PDF first." : "Hãy chọn PDF trước.");
      const result = await readPrintedPdf(id, intent, owner.signal);
      let parties: PdfPartyResolution | undefined;
      if (
        onApply &&
        (result.suggestions.senderName ||
          result.suggestions.recipientNames?.length)
      ) {
        if (active())
          setState({
            key,
            owner,
            busy: true,
            notice: isEn
              ? "Matching existing organizations…"
              : "Đang đối chiếu đơn vị có sẵn…",
          });
        parties = await resolvePdfParties(
          result.suggestions,
          distributionTargets ?? [],
          owner.signal,
        );
      }
      intent.assertCurrent();
      if (active()) {
        setState({ key, owner, result, name, parties });
        if (file) callbacks.current.onFileReady?.(id);
      }
    } catch (error) {
      if (active()) {
        const code = error instanceof ApiRequestError ? error.code : undefined;
        const message =
          code === "OCR_DISABLED" || code === "OCR_ENGINE_UNAVAILABLE"
            ? isEn
              ? "Printed PDF recognition is not configured."
              : "Bộ đọc PDF chữ in chưa được cấu hình."
            : code === "OCR_BUSY"
              ? isEn
                ? "Another PDF is being processed. Try again shortly."
                : "Đang xử lý một PDF khác. Vui lòng thử lại sau ít giây."
              : error instanceof ApiRequestError &&
                  [403, 404].includes(error.status)
                ? isEn
                  ? "You cannot read this PDF."
                  : "Bạn không được đọc PDF này."
                : error instanceof ApiRequestError && error.status === 423
                  ? isEn
                    ? "PDF safety scan has not completed. Try again after the scan."
                    : "PDF chưa quét xong. Vui lòng thử lại sau khi hoàn tất."
                  : isEn
                    ? "PDF could not be read. It must be a valid printed PDF of at most 25 MB / 100 pages."
                    : "Không thể đọc PDF. Cần PDF chữ in hợp lệ, tối đa 25 MB / 100 trang.";
        setState({ key, owner, error: message });
      }
    } finally {
      if (active()) {
        guard.current = false;
        setState((s) =>
          s.key === key && s.owner === owner ? { ...s, busy: false } : s,
        );
        callbacks.current.onBusy?.(false);
      }
    }
  }
  return (
    <div className="flex flex-col gap-3 p-4 border rounded">
      <Typography variant="h6">
        {isEn ? "Printed PDF recognition" : "Đọc PDF chữ in"}
      </Typography>
      <Typography variant="body2">
        {isEn
          ? "Reads embedded text first; OCR is used for scanned pages. Review suggestions before registering. Handwriting is not supported."
          : "Ưu tiên văn bản sẵn có; OCR dùng cho trang scan. Kiểm tra gợi ý trước khi đăng ký. Không nhận dạng chữ viết tay."}
      </Typography>
      {fileId ? (
        <Button
          type="button"
          variant="outlined"
          disabled={disabled || !!current?.busy}
          onClick={() => scan()}
        >
          {isEn ? "Read printed PDF" : "Đọc PDF chữ in"}
        </Button>
      ) : (
        <>
        <Button
          type="button"
          variant="outlined"
          onClick={() => fileInput.current?.click()}
          disabled={disabled || !!current?.busy}
        >
          {isEn ? "Select PDF and read" : "Chọn PDF và đọc"}
        </Button>
          <input
            ref={fileInput}
            type="file"
            accept=".pdf,application/pdf"
            hidden
            aria-label={isEn ? "Select printed PDF" : "Chọn PDF chữ in"}
            onChange={(e) => {
              const file = e.target.files?.[0];
              e.target.value = "";
              if (file) void scan(file);
            }}
          />
        </>
      )}
      {current?.busy && (
        <div role="status" className="flex items-center gap-2">
          <CircularProgress size={20} />
          <Typography>{current.notice}</Typography>
        </div>
      )}
      {current?.error && <Alert severity="warning">{current.error}</Alert>}
      {current?.result && (
        <>
          <Alert severity="info">
            {isEn
              ? "Recognition is a suggestion. No document number has been issued by OCR."
              : "Nhận dạng chỉ là gợi ý. OCR không tự cấp số công văn."}
          </Alert>
          <Typography variant="body2">
            {current.name ?? ""} · {current.result.pages.length}{" "}
            {isEn ? "pages" : "trang"} ·{" "}
            {current.result.pages.filter((p) => p.source === "OCR").length}{" "}
            {isEn ? "OCR pages" : "trang OCR"}
          </Typography>
          {current.result.warnings.includes("NO_TEXT_RECOGNIZED") && (
            <Alert severity="warning">
              {isEn
                ? "No text recognized; enter document fields manually."
                : "Không nhận dạng được văn bản; hãy nhập các trường công văn thủ công."}
            </Alert>
          )}
          <TextField
            fullWidth
            multiline
            minRows={4}
            maxRows={12}
            label={isEn ? "Extracted text" : "Văn bản đọc được"}
            value={current.result.text}
            slotProps={{ input: { readOnly: true } }}
          />
          <Typography className="break-words">
            {isEn ? "Reference" : "Số hiệu"}:{" "}
            {current.result.suggestions.referenceNumber ?? "—"} ·{" "}
            {isEn ? "Issued date" : "Ngày phát hành"}:{" "}
            {current.result.suggestions.issuedDate ?? "—"} ·{" "}
            {isEn ? "Subject" : "Trích yếu"}:{" "}
            {current.result.suggestions.subject ?? "—"}
          </Typography>
          {onApply && (
            <>
              {
                <div className="flex flex-col gap-2">
                  <Typography variant="body2">
                    {isEn ? "Sender on PDF" : "Đơn vị gửi trên PDF"}:{" "}
                    {current.result.suggestions.senderName ??
                      (isEn
                        ? "Not identified — select manually"
                        : "Chưa xác định — tự chọn")}
                  </Typography>
                  <Typography variant="body2">
                    {isEn ? "Recipients on PDF" : "Đơn vị nhận trên PDF"}:{" "}
                    {current.result.suggestions.recipientNames?.join("; ") ||
                      (isEn
                        ? "Not identified — select manually"
                        : "Chưa xác định — tự chọn")}
                  </Typography>
                  {current.parties?.sender && (
                    <Alert
                      severity={
                        current.parties.sender.status === "matched"
                          ? "success"
                          : "info"
                      }
                    >
                      {current.parties.sender.status === "matched"
                        ? (isEn ? "Matched sender: " : "Đơn vị gửi đã khớp: ") +
                          current.parties.sender.option!.name
                        : isEn
                          ? "No unique sender match. Choose the sender in the form below."
                          : "Chưa có một đơn vị gửi khớp duy nhất. Hãy tự chọn Đơn vị gửi trong form bên dưới."}
                    </Alert>
                  )}
                  {!!current.parties?.recipients.length && (
                    <Alert
                      severity={
                        current.parties.recipients.every(
                          (row) => row.status === "matched",
                        )
                          ? "success"
                          : "info"
                      }
                    >
                      {isEn ? "DAS receiving units" : "Nơi nhận DAS"}:{" "}
                      {current.parties.recipients
                        .map(
                          (row) =>
                            `${row.option?.name ?? row.name} (${row.status === "matched" ? (isEn ? "matched" : "đã khớp") : isEn ? "select manually" : "tự chọn"})`,
                        )
                        .join("; ")}
                    </Alert>
                  )}
                </div>
              }
              <Button
                type="button"
                disabled={
                  disabled || current.busy || !current.result.text.trim()
                }
                onClick={() => {
                  intent.assertCurrent();
                  if (active() && current.result)
                    callbacks.current.onApply?.(
                      current.result,
                      current.parties,
                    );
                }}
              >
                {isEn ? "Apply suggestions" : "Áp dụng gợi ý"}
              </Button>
            </>
          )}
        </>
      )}
    </div>
  );
}
