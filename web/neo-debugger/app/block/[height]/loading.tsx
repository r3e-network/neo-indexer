export default function Loading() {
  return (
    <div className="min-h-screen bg-slate-950 text-slate-200 p-10">
      <div className="h-6 w-32 bg-slate-800 rounded mb-4 animate-pulse" />
      <div className="space-y-3">
        {[...Array(5)].map((_, i) => (
          <div key={i} className="h-10 bg-slate-900 rounded animate-pulse" />
        ))}
      </div>
    </div>
  );
}
