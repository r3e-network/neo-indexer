import { TraceBrowserControls } from './traceBrowser/TraceBrowserControls';
import { TraceBrowserSidebar } from './traceBrowser/TraceBrowserSidebar';
import { TraceBrowserTabs } from './traceBrowser/TraceBrowserTabs';
import { useTraceBrowser } from './traceBrowser/useTraceBrowser';

export default function TraceBrowser() {
  const model = useTraceBrowser();

  return (
    <div className="mx-auto max-w-7xl space-y-6 px-4 py-8 text-white">
      <TraceBrowserControls model={model} />
      <div className="grid gap-6 lg:grid-cols-[320px,1fr]">
        <TraceBrowserSidebar model={model} />
        <TraceBrowserTabs model={model} />
      </div>
    </div>
  );
}
