import { Card, CardContent } from "@/components/ui/card";

export default function SettingsPage() {
  return (
    <div className="p-8">
      <div className="mb-8">
        <h1 className="text-2xl font-semibold text-foreground">Settings</h1>
        <p className="mt-1 text-muted-foreground">
          Company info, punch rules, and configuration
        </p>
      </div>
      <Card>
        <CardContent className="py-6">
          <p className="text-muted-foreground">Settings will appear here.</p>
        </CardContent>
      </Card>
    </div>
  );
}
