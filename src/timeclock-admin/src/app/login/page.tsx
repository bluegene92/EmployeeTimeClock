"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { Clock } from "lucide-react";
import { ThemeToggle } from "@/components/ThemeToggle";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";

const FAKE_USER = { username: "admin", password: "admin123" };

export default function LoginPage() {
  const router = useRouter();
  useEffect(() => {
    const auth = sessionStorage.getItem("auth");
    if (auth) router.replace("/dashboard/employees");
  }, [router]);
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    if (username === FAKE_USER.username && password === FAKE_USER.password) {
      sessionStorage.setItem("auth", JSON.stringify({ user: username }));
      router.push("/dashboard/employees");
    } else {
      setError("Invalid username or password");
    }
  };

  const handleGoogleLogin = () => {
    sessionStorage.setItem("auth", JSON.stringify({ user: "google@example.com" }));
    router.push("/dashboard/employees");
  };

  return (
    <div className="relative flex min-h-screen items-center justify-center bg-gradient-to-br from-neutral-50 via-white to-neutral-100 dark:from-neutral-950 dark:via-neutral-900 dark:to-neutral-950">
      <div className="absolute right-4 top-4">
        <ThemeToggle />
      </div>
      <div className="w-full max-w-md">
        <Card className="border-neutral-200/80 dark:border-neutral-800">
          <CardHeader className="text-center">
            <div className="mb-4 flex justify-center">
              <div className="flex h-12 w-12 items-center justify-center rounded-md bg-primary text-primary-foreground">
                <Clock className="h-6 w-6" />
              </div>
            </div>
            <CardTitle className="text-2xl">TimeClock Admin</CardTitle>
            <CardDescription>Sign in to manage your workforce</CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit} className="space-y-5">
              {error && (
                <div className="rounded-md bg-destructive/10 px-4 py-3 text-sm text-destructive">
                  {error}
                </div>
              )}
              <div className="space-y-2">
                <Label htmlFor="username">Username</Label>
                <Input
                  id="username"
                  type="text"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  placeholder="Enter your username"
                  required
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="password">Password</Label>
                <Input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Enter your password"
                  required
                />
              </div>
              <Button type="submit" className="w-full" size="lg">
                Sign in
              </Button>
            </form>

            <div className="mt-6">
              <Separator className="my-4" />
              <div className="relative flex justify-center text-sm">
                <span className="bg-card px-4 text-muted-foreground">or continue with</span>
              </div>
              <Button
                type="button"
                variant="outline"
                className="mt-4 w-full"
                size="lg"
                onClick={handleGoogleLogin}
              >
                <svg className="mr-2 h-5 w-5" viewBox="0 0 24 24">
                  <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" />
                  <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" />
                  <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" />
                  <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" />
                </svg>
                Sign in with Google
              </Button>
            </div>
          </CardContent>
        </Card>
        <p className="mt-6 text-center text-xs text-muted-foreground">
          Demo: admin / admin123
        </p>
      </div>
    </div>
  );
}
