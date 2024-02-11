import { hubBaseUrl } from '../config';
import * as session from './session';
import {
  HubConnectionBuilder,
  JsonHubProtocol,
  LogLevel,
} from '@microsoft/signalr';

export const createHubConnection = ({ url }) =>
  new HubConnectionBuilder()
    .withUrl(url, {
      accessTokenFactory: session.isPassthroughEnabled()
        ? undefined
        : session.getToken,
      withCredentials: true,
    })
    .withAutomaticReconnect([
      0, 100, 250, 500, 1_000, 2_000, 3_000, 5_000, 5_000, 5_000, 5_000, 5_000,
    ])
    .withHubProtocol(new JsonHubProtocol())
    .configureLogging(LogLevel.Warning)
    .build();

export const createApplicationHubConnection = () =>
  createHubConnection({ url: `${hubBaseUrl}/application` });

export const createLogsHubConnection = () =>
  createHubConnection({ url: `${hubBaseUrl}/logs` });

export const createSearchHubConnection = () =>
  createHubConnection({ url: `${hubBaseUrl}/search` });
