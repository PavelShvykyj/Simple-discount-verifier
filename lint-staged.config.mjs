const quote = (value) => `"${value.replaceAll('"', '\\"')}"`;

export default {
  'frontend/**/*.{ts,html}': (files) => {
    const frontendFiles = files.map(quote).join(' ');

    return `npm --prefix frontend exec eslint -- ${frontendFiles}`;
  }
};
