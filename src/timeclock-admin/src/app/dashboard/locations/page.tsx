"use client";

import { useEffect, useState } from "react";
import { Pencil, Plus, Trash2 } from "lucide-react";
import type { Location } from "@/types/employee";
import { createLocation, deleteLocation, listLocations, updateLocation } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Card, CardContent } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { cn } from "@/lib/utils";

export default function LocationsPage() {
  const [locations, setLocations] = useState<Location[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState<Location | "new" | null>(null);

  const refresh = async () => setLocations(await listLocations());

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const data = await listLocations();
        if (!cancelled) {
          setLocations(data);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Failed to load locations.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const handleSave = async (name: string, isActive: boolean) => {
    try {
      setBusy(true);
      if (editing === "new") await createLocation({ name, isActive });
      else if (editing) await updateLocation(editing.id, { name, isActive });
      await refresh();
      setEditing(null);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save location.");
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async (location: Location) => {
    try {
      setBusy(true);
      await deleteLocation(location.id);
      await refresh();
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete location.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Locations</h1>
          <p className="mt-1 text-muted-foreground">
            Restaurants or sites employees are assigned to. They can punch at any location.
          </p>
        </div>
        <Button onClick={() => setEditing("new")}>
          <Plus className="mr-2 h-4 w-4" />
          Add location
        </Button>
      </div>

      {error && (
        <p className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      )}

      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-28" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {locations.map((location) => (
                <TableRow key={location.id}>
                  <TableCell className="font-medium">{location.name}</TableCell>
                  <TableCell>
                    <span
                      className={cn(
                        "inline-flex rounded-md px-2.5 py-0.5 text-xs font-medium",
                        location.isActive
                          ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
                          : "bg-muted text-muted-foreground"
                      )}
                    >
                      {location.isActive ? "Active" : "Inactive"}
                    </span>
                  </TableCell>
                  <TableCell>
                    <div className="flex justify-end gap-1">
                      <Button variant="ghost" size="icon" className="h-8 w-8" onClick={() => setEditing(location)}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8 text-destructive hover:bg-destructive/10 hover:text-destructive"
                        disabled={busy}
                        onClick={() => handleDelete(location)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          {loading && <div className="px-8 py-12 text-center text-muted-foreground">Loading locations...</div>}
          {!loading && locations.length === 0 && (
            <div className="px-8 py-12 text-center text-muted-foreground">No locations yet.</div>
          )}
        </CardContent>
      </Card>

      {editing && (
        <LocationDialog
          location={editing === "new" ? null : editing}
          busy={busy}
          onClose={() => setEditing(null)}
          onSave={handleSave}
        />
      )}
    </div>
  );
}

function LocationDialog({
  location,
  busy,
  onClose,
  onSave,
}: {
  location: Location | null;
  busy: boolean;
  onClose: () => void;
  onSave: (name: string, isActive: boolean) => void;
}) {
  const [name, setName] = useState(location?.name ?? "");
  const [isActive, setIsActive] = useState(location?.isActive ?? true);

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{location ? "Edit location" : "Add location"}</DialogTitle>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            onSave(name, isActive);
          }}
        >
          <div className="space-y-2">
            <Label>Name</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Restaurant 1" required />
          </div>
          <div className="flex items-center gap-2">
            <Checkbox id="location-active" checked={isActive} onCheckedChange={(c) => setIsActive(!!c)} />
            <Label htmlFor="location-active">Active</Label>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={busy}>Cancel</Button>
            <Button type="submit" disabled={busy}>Save</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
