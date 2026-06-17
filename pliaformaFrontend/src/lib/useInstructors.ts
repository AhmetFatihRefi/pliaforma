"use client";

import { useEffect, useState } from "react";
import { instructorService, memberService } from "./studioApi";
import { useRole } from "./useRole";

export interface InstructorOption {
  id: string; // InstructorProfileId
  name: string;
}

/**
 * Ders/müsaitlik ekranları için hoca listesi.
 * Owner/StudioAdmin: tüm hocalar (üyelik listesinden). Instructor: yalnızca kendisi.
 */
export function useInstructors() {
  const { canManage } = useRole();
  const [instructors, setInstructors] = useState<InstructorOption[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        if (canManage) {
          const members = await memberService.list(false);
          const opts = members
            .filter((m) => m.instructorProfileId)
            .map((m) => ({ id: m.instructorProfileId as string, name: m.fullName }));
          if (!cancelled) setInstructors(opts);
        } else {
          const me = await instructorService.me();
          if (!cancelled) setInstructors([{ id: me.instructorProfileId, name: me.displayName }]);
        }
      } catch {
        if (!cancelled) setInstructors([]);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canManage]);

  return { instructors, loading, isManager: canManage };
}
