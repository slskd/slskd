/** @type {import('eslint').Linter.Config} */
module.exports = {
  extends: ['canonical/auto', 'canonical/browser', 'canonical/node'],
  ignorePatterns: ['build', 'node_modules', 'package-lock.json'],
  overrides: [
    {
      extends: [
        'canonical',
        'canonical/regexp',
        'canonical/jsdoc',
        'canonical/jsx-a11y',
        'canonical/react',
        'canonical/prettier',
      ],
      files: ['*.jsx'],
      parserOptions: {
        babelOptions: {
          parserOpts: {
            plugins: ['jsx'],
          },
        },
      },
    },
    {
      extends: ['canonical/jest'],
      files: '*.test.{js,jsx}',
    },
  ],
  root: true,
};
