# Running Behind a Reverse Proxy

## NGINX

Because slskd uses websockets in addition to standard HTTP traffic, a couple of headers need to be defined along with the standard `proxy_pass` to enable everything to work properly.

Assuming a default NGINX installation, create `/etc/nginx/conf.d/slskd.conf` and populate it with one of the configurations below, depending on whether you'd like to serve slskd from a subdirectory or the root of the webserver.

### At a Subdirectory

In this scenario `URL_BASE` must be set to `/slskd`

```
server {
        listen 8080;

        location /slskd {
                proxy_pass http://localhost:5030;
                proxy_set_header Upgrade $http_upgrade;
                proxy_set_header Connection "Upgrade";
                proxy_set_header Host $host;
                proxy_request_buffering off;
        }

        client_max_body_size 0;
}
```

### At the Root

In this scenario `URL_BASE` is left to the default value of `/`

```
server {
        listen 8080;

        location / {
                proxy_pass http://localhost:5030;
                proxy_set_header Upgrade $http_upgrade;
                proxy_set_header Connection "Upgrade";
                proxy_set_header Host $host;
                proxy_request_buffering off;
        }

        client_max_body_size 0;
}
```

## SWAG

### At a Subdomain

Create a file named "slskd.subdomain.conf" in /PATH_TO_SWAG/nginx/proxy-confs/

Here's an example, you may need to change the port.


```
# make sure that your slskd container is named slskd
# make sure that your dns has a cname set for slskd

server {
    listen 443 ssl;
    listen [::]:443 ssl;

    server_name slskd.*;

    include /config/nginx/ssl.conf;

    client_max_body_size 0;

    # enable for ldap auth (requires ldap-location.conf in the location block)
    #include /config/nginx/ldap-server.conf;

    # enable for Authelia (requires authelia-location.conf in the location block)
    #include /config/nginx/authelia-server.conf;

    # enable for Authentik (requires authentik-location.conf in the location block)
    #include /config/nginx/authentik-server.conf;

    location / {
        # enable the next two lines for http auth
        #auth_basic "Restricted";
        #auth_basic_user_file /config/nginx/.htpasswd;

        # enable for ldap auth (requires ldap-server.conf in the server block)
        #include /config/nginx/ldap-location.conf;

        # enable for Authelia (requires authelia-server.conf in the server block)
        #include /config/nginx/authelia-location.conf;

        # enable for Authentik (requires authentik-server.conf in the server block)
        #include /config/nginx/authentik-location.conf;

        include /config/nginx/proxy.conf;
        include /config/nginx/resolver.conf;
        set $upstream_app slskd;
        set $upstream_port 5030;
        set $upstream_proto http;
        proxy_pass $upstream_proto://$upstream_app:$upstream_port;

    }

}
```


## Apache

With `URL_BASE` set to `/slskd`:

```
ProxyPass /slskd/ http://the.local.ip.address:5030/slskd/ upgrade=websocket
ProxyPassReverse /slskd/ http://the.local.ip.address:5030/slskd/

```

From [discussion #890](https://github.com/slskd/slskd/discussions/890)

## IIS (Windows)

You'll need the [URL rewrite module](https://learn.microsoft.com/en-us/iis/extensions/url-rewrite-module/using-the-url-rewrite-module) installed before you begin, and you'll need to set `URL_BASE` to `/slskd` in the slskd config.

Update the `web.config` for your root site to add a rewrite rule.  Here's an example that rewrites `<your site>/slskd`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
        <rules>
	    <rule name="slskd" stopProcessing="true">
                <match url="slskd(/.*)" />
                <action type="Rewrite" url="http://<ip of slskd>:<port of slskd>/slskd{R:1}" />
            </rule>
        </rules>
    </rewrite>
  </system.webServer>
</configuration>
```
