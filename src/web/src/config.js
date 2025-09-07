const urlBase = (window.urlBase === '/' ? '' : window.urlBase) || '';
const developmentPort = window.port ?? 5_030;
const rootUrl =
  // eslint-disable-next-line n/no-process-env
  process.env.NODE_ENV === 'production'
    ? urlBase
    : `http://localhost:${developmentPort}${urlBase}`;
const apiBaseUrl = `${rootUrl}/api/v0`;
const hubBaseUrl = `${rootUrl}/hub`;
const tokenKey = 'slskd-token';
const tokenPassthroughValue = 'n/a';
const activeChatKey = 'slskd-active-chat';
const activeRoomKey = 'slskd-active-room';
const activeUserInfoKey = 'slskd-active-user';
const defaultFilter = window.defaultFilter ?? '';

export {
  activeChatKey,
  activeRoomKey,
  activeUserInfoKey,
  apiBaseUrl,
  defaultFilter,
  hubBaseUrl,
  rootUrl,
  tokenKey,
  tokenPassthroughValue,
  urlBase,
};
