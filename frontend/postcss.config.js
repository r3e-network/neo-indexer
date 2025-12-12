import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);

function hasModule(name) {
  try {
    require.resolve(name);
    return true;
  } catch (error) {
    return false;
  }
}

const plugins = {};
if (hasModule('tailwindcss')) {
  plugins.tailwindcss = {};
}
if (hasModule('autoprefixer')) {
  plugins.autoprefixer = {};
}

export default { plugins };
