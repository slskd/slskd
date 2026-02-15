# Running with a VPN

## Choose a Provider

To make the most out of your configuration you'll want to choose a VPN provider that supports port forwarding.  While not a necessity, a properly forwarded port ensures that you can connect to everyone on the network.

The following are referral links for providers:

* [Proton VPN](https://pr.tn/ref/B0CVVTZ9): Use the link to get 2 free weeks, $20 off of your bill and $20 in credits for the maintainer

## Gluetun

To use the built-in gluetun VPN integration, gluetun's [control server](https://github.com/qdm12/gluetun-wiki/blob/main/setup/advanced/control-server.md) needs to be enabled.  There are several options for authentication; API keys are covered here.

Set:

* `GLUETUN_HTTP_CONTROL_SERVER_ENABLE` to `on`
* `HTTP_CONTROL_SERVER_AUTH_DEFAULT_ROLE` to `'{"auth":"apikey","apikey":"abcd123"}'` (choose whatever key you want)

For slskd, set:

* `SLSKD_VPN` to `true` to enable the VPN integration
* `SLSKD_VPN_PORT_FORWARDING` to `true` to force slskd to wait for gluetun to provide a port
* `SLSKD_VPN_GLUETUN_URL` to `http://localhost:8000`, the default URL for gluetun's control server
* `SLSKD_VPN_GLUETUN_API_KEY` to `abcd123` (or whatever your gluetun key is)

Here's a reference `docker-compose.yml` file that uses Proton VPN.

```yaml
version: "3"
services:
  gluetun:
    image: qmcgaw/gluetun
    container_name: gluetun
    cap_add:
      - NET_ADMIN
    devices:
      - /dev/net/tun:/dev/net/tun
    ports:
      - 8000:8000/tcp
      - 5030:5030/tcp
    environment:
      - VPN_SERVICE_PROVIDER=protonvpn
      - VPN_TYPE=wireguard
      - WIREGUARD_PRIVATE_KEY=<your key>
      - SERVER_COUNTRIES=Netherlands
      - VPN_PORT_FORWARDING=on
      - GLUETUN_HTTP_CONTROL_SERVER_ENABLE=on
      - HTTP_CONTROL_SERVER_AUTH_DEFAULT_ROLE='{"auth":"apikey","apikey":"abcd123"}'
    restart: unless-stopped
  slskd:
    image: slskd/slskd:latest
    container_name: slskd
    network_mode: service:gluetun
    depends_on:
      - gluetun
    environment:
      - SLSKD_SLSK_USERNAME=<your username>
      - SLSKD_SLSK_PASSWORD=<your password>
      - SLSKD_VPN=true
      - SLSKD_VPN_PORT_FORWARDING=true
      - SLSKD_VPN_GLUETUN_URL=http://localhost:8000
      - SLSKD_VPN_GLUETUN_API_KEY=abcd123
    volumes:
      - ~/slskd/:/app
    restart: unless-stopped
```