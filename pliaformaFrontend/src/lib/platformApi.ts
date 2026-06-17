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
  SubscriptionPaymentDto,
  SubscriptionPlanDto,
  SubscriptionPlanInput,
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

  // --- Plan kataloğu ---
  plans: (onlyActive?: boolean) =>
    api<SubscriptionPlanDto[]>(`/api/platform/plans${onlyActive ? "?onlyActive=true" : ""}`),
  createPlan: (input: SubscriptionPlanInput) =>
    api<SubscriptionPlanDto>("/api/platform/plans", { method: "POST", body: input }),
  updatePlan: (id: string, input: SubscriptionPlanInput) =>
    api<SubscriptionPlanDto>(`/api/platform/plans/${id}`, { method: "PUT", body: input }),
  deletePlan: (id: string) => api<null>(`/api/platform/plans/${id}`, { method: "DELETE" }),

  // --- Stüdyo aboneliği ---
  getSubscription: (id: string) =>
    api<PlatformSubscriptionDto | null>(`/api/platform/studios/${id}/subscription`),
  updateSubscription: (id: string, input: SubscriptionInput) =>
    api<PlatformSubscriptionDto>(`/api/platform/studios/${id}/subscription`, { method: "PUT", body: input }),
  assignPlan: (id: string, planId: string) =>
    api<PlatformSubscriptionDto>(`/api/platform/studios/${id}/subscription/plan`, { method: "PUT", body: { planId } }),
  recordPayment: (id: string, input: { amount?: number | null; notes?: string } = {}) =>
    api<PlatformSubscriptionDto>(`/api/platform/studios/${id}/subscription/record-payment`, { method: "POST", body: input }),
  subscriptionPayments: (id: string) =>
    api<SubscriptionPaymentDto[]>(`/api/platform/studios/${id}/subscription/payments`),
  cancelSubscription: (id: string) =>
    api<PlatformSubscriptionDto>(`/api/platform/studios/${id}/subscription/cancel`, { method: "POST", body: {} }),

  healthSummary: () => api<PlatformHealthSummaryDto>("/api/platform/health-summary"),

  recentActivity: () => api<PlatformActivityItemDto[]>("/api/platform/recent-activity"),
};
