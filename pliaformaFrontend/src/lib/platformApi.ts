import { api } from "./api";
import type {
  PlatformActivityItemDto,
  PlatformHealthSummaryDto,
  PlatformOverviewDto,
  PlatformStudioDetailDto,
  PlatformStudioListItemDto,
  PlatformStudioStatusDto,
  PlatformSubscriptionDto,
  StudioStatus,
  SubscriptionInput,
} from "./domain-types";

// Platform (cross-tenant) yönetim uçları (/api/platform/*) — yalnızca PlatformAdmin.
export const platformService = {
  overview: () => api<PlatformOverviewDto>("/api/platform/overview"),

  studios: (params: { search?: string; status?: StudioStatus } = {}) => {
    const q = new URLSearchParams();
    if (params.search) q.set("search", params.search);
    if (params.status) q.set("status", params.status);
    const qs = q.toString();
    return api<PlatformStudioListItemDto[]>(`/api/platform/studios${qs ? `?${qs}` : ""}`);
  },

  studioDetail: (id: string) => api<PlatformStudioDetailDto>(`/api/platform/studios/${id}`),

  suspend: (id: string, reason: string) =>
    api<PlatformStudioStatusDto>(`/api/platform/studios/${id}/suspend`, { method: "POST", body: { reason } }),

  reactivate: (id: string) =>
    api<PlatformStudioStatusDto>(`/api/platform/studios/${id}/reactivate`, { method: "POST", body: {} }),

  getSubscription: (id: string) =>
    api<PlatformSubscriptionDto | null>(`/api/platform/studios/${id}/subscription`),

  updateSubscription: (id: string, input: SubscriptionInput) =>
    api<PlatformSubscriptionDto>(`/api/platform/studios/${id}/subscription`, { method: "PUT", body: input }),

  cancelSubscription: (id: string) =>
    api<PlatformSubscriptionDto>(`/api/platform/studios/${id}/subscription/cancel`, { method: "POST", body: {} }),

  healthSummary: () => api<PlatformHealthSummaryDto>("/api/platform/health-summary"),

  recentActivity: () => api<PlatformActivityItemDto[]>("/api/platform/recent-activity"),
};
