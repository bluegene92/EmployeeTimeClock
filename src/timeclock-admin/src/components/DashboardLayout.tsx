"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import {
  Clock,
  Users,
  BarChart3,
  Settings,
  LogOut,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import { ThemeToggle } from "./ThemeToggle";
import { Button } from "./ui/button";
import { cn } from "@/lib/utils";

const NAV_ITEMS = [
  { href: "/dashboard/employees", label: "Employees", icon: Users },
  { href: "/dashboard/timeclock", label: "Time Clock", icon: Clock },
  { href: "/dashboard/reports", label: "Reports", icon: BarChart3 },
  { href: "/dashboard/settings", label: "Settings", icon: Settings },
];

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const pathname = usePathname();
  const router = useRouter();
  const [mounted, setMounted] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  useEffect(() => setMounted(true), []);

  useEffect(() => {
    if (!mounted) return;
    const auth = sessionStorage.getItem("auth");
    if (!auth) router.replace("/login");
  }, [mounted, router]);

  const handleLogout = () => {
    sessionStorage.removeItem("auth");
    router.replace("/login");
  };

  if (!mounted) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-primary border-t-transparent" />
      </div>
    );
  }

  return (
    <div className="flex min-h-screen bg-muted/30 dark:bg-background">
      {/* Sidebar */}
      <aside
        className={cn(
          "flex shrink-0 flex-col border-r border-border bg-card transition-all duration-300",
          sidebarCollapsed ? "w-16" : "w-64"
        )}
      >
        <div
          className={cn(
            "flex h-16 items-center border-b border-border",
            sidebarCollapsed ? "justify-center px-0" : "gap-2 px-6"
          )}
        >
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-primary text-primary-foreground">
            <Clock className="h-4 w-4" />
          </div>
          {!sidebarCollapsed && (
            <span className="font-semibold text-foreground">TimeClock</span>
          )}
        </div>
        <nav className="flex-1 space-y-0.5 p-4">
          {NAV_ITEMS.map((item) => {
            const isActive =
              pathname === item.href ||
              pathname.startsWith(item.href + "/");
            const Icon = item.icon;
            return (
              <Link key={item.href} href={item.href} title={sidebarCollapsed ? item.label : undefined}>
                <Button
                  variant={isActive ? "secondary" : "ghost"}
                  className={cn(
                    "w-full justify-start gap-3 rounded-md",
                    sidebarCollapsed && "justify-center px-2"
                  )}
                >
                  <Icon className="h-5 w-5 shrink-0" />
                  {!sidebarCollapsed && item.label}
                </Button>
              </Link>
            );
          })}
        </nav>
        <div className="space-y-1 border-t border-border p-4">
          <Button
            variant="ghost"
            className={cn(
              "w-full justify-start gap-3 rounded-md",
              sidebarCollapsed && "justify-center px-2"
            )}
            onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
            title={sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar"}
          >
            {sidebarCollapsed ? (
              <ChevronRight className="h-5 w-5 shrink-0" />
            ) : (
              <>
                <ChevronLeft className="h-5 w-5 shrink-0" />
                Collapse
              </>
            )}
          </Button>
          <Button
            variant="ghost"
            className={cn(
              "w-full justify-start gap-3 rounded-md",
              sidebarCollapsed && "justify-center px-2"
            )}
            onClick={handleLogout}
            title={sidebarCollapsed ? "Sign out" : undefined}
          >
            <LogOut className="h-5 w-5 shrink-0" />
            {!sidebarCollapsed && "Sign out"}
          </Button>
        </div>
      </aside>

      {/* Main area with top bar */}
      <div className="flex flex-1 flex-col overflow-hidden">
        <header className="flex h-16 shrink-0 items-center justify-between border-b border-border bg-card px-6">
          <div className="h-6 w-6" />
          <ThemeToggle />
        </header>
        <main className="flex-1 overflow-auto">{children}</main>
      </div>
    </div>
  );
}
