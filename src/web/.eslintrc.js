/** @type {import('eslint').Linter.Config} */
module.exports = {
  extends: ['canonical/auto', 'canonical/browser', 'canonical/node'],
  ignorePatterns: ['build'],
  overrides: [
    {
      extends: ['canonical/jsx-a11y'],
      files: '*.jsx',
    },
    {
      extends: ['canonical/jest'],
      files: '*.test.{js,jsx}',
    },
  ],
  root: true,
};
