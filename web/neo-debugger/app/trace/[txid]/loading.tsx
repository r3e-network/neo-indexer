export default function Loading() {
  return (
    <div className="min-h-screen bg-slate-950 text-slate-200 p-10">
      <div className="h-6 w-48 bg-slate-800 rounded mb-6 animate-pulse" />
      <div className="space-y-2">
        {[...Array(8)].map((_, i) => (
          <div key={i} className="h-8 bg-slate-900 rounded animate-pulse" />
        ))}
      </div>
    </div>
  );
}
