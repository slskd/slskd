const urlBase = (window.urlBase === '/' ? '' : window.urlBase) || '';
const developmentPort = window.port ?? 5_030;
const rootUrl =
  // eslint-disable-next-line n/no-process-env
  process.env.NODE_ENV === 'production'
    ? urlBase
    : `http://localhost:${developmentPort}${urlBase}`;

const apiBaseUrlFromLocalStorage = localStorage.getItem('apiBaseUrl');

if (apiBaseUrlFromLocalStorage) {
  console.log(
    'Overriding apiBaseUrl from local storage',
    apiBaseUrlFromLocalStorage,
  );
}

const apiBaseUrl = `${apiBaseUrlFromLocalStorage || rootUrl}/api/v0`;
const hubBaseUrl = `${apiBaseUrlFromLocalStorage || rootUrl}/hub`;
const tokenKey = 'slskd-token';
const tokenPassthroughValue = 'n/a';
const activeChatKey = 'slskd-active-chat';
const activeRoomKey = 'slskd-active-room';
const activeUserInfoKey = 'slskd-active-user';

export {
  activeChatKey,
  activeRoomKey,
  activeUserInfoKey,
  apiBaseUrl,
  hubBaseUrl,
  rootUrl,
  tokenKey,
  tokenPassthroughValue,
  urlBase,
};
