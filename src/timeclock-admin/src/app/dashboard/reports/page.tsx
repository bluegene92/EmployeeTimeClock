import { Card, CardContent } from "@/components/ui/card";

export default function ReportsPage() {
  return (
    <div className="p-8">
      <div className="mb-8">
        <h1 className="text-2xl font-semibold text-foreground">Reports</h1>
        <p className="mt-1 text-muted-foreground">
          View hours worked, overtime, and export for payroll
        </p>
      </div>
      <Card>
        <CardContent className="py-6">
          <p className="text-muted-foreground">
            Reports will appear here. Connect to API to load data.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
