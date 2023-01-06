# Relay Mode

## What is Relay Mode?

Relay mode refers to an optional configuration that allows two or more instances of slskd to work together over a network as one client.

A relay configuration consists of one **Controller** and one or more **Agents**.  

The controller is the instance that connects to the Soulseek network and acts as the client; it's pretty much a standard instance of slskd.  Agents are additional instances that connect to the controller, instead of the Soulseek network.  These instances only relay files to the controller.

You can think of Relay agents as 'remote storage' for slskd.

Here's an example of a Relay configuration with one agents running in a user's home and a controller that's running on a server somewhere in the cloud:


```mermaid
C4Context
    Boundary(Home, "Home", ""){
        System(Agent, "Agent", "Connects to controller")
    }

    Boundary(Internet, "Internet", "") {
        System(Controller, "Controller", "Connects to the Soulseek network")

        Boundary(Soulseek, "Soulseek Network", ""){
            System(Other, "Other Clients", "")
        }
    }

    Rel(Agent, Controller, "Files")
    BiRel(Controller, Other, "Soulseek Traffic")

    UpdateRelStyle(Agent, Controller, $offsetY="-20", $offsetX="-10")
    UpdateRelStyle(Controller, Other, $offsetY="-50")
```

## Why use Relay Mode?

There's a number of reasons someone would want to use Relay mode:

* You want to serve files from multiple locations, but don't want to have multiple Soulseek logins
* You are concerned about privacy and don't want to use (or don't trust) a VPN
* You want to want full functionality on the Soulseek network but can't configure port forwarding properly
* You are a high volume user and want to offload bandwidth from your home internet connection
* You want an always-on Soulseek client to preserve your place in remote queues, and don't have an always-on client at home

For most of these use cases you can simply run an instance of slskd in the cloud, but that can get expensive and painful to manage if you're sharing a large number of files and/or add new files to your shares regularly.  With a Relay you can point an agent to your local files and connect it to your cloud instance and get the best of both worlds.

## Is Relay Mode Secure?

Extra secure!  

All traffic between the Relay controller and agents is (optionally) sent over HTTPS, meaning it is end to end encrypted.

Agents connect to the controller, not the other way around, meaning your home network doesn't need to forward any ports or enable any additional incoming traffic through firewalls.  The only trust involved is in the address of the controller, which you control.

All incoming requests to the controller must be secured with an API key, and each agent is configured with an additional shared secret to ensure authenticity.  Agent configuration and API keys can be scoped to a specific IP address or CIDR block to ensure only authorized agents can connect, even if API keys and shared secrets are obtained by an attacker.

## Quick Start

To start, you'll need two instances of slskd running on two different machines. Determine which is the controller (the instance that will connect to the Soulseek network) and which is the agent (the instance that will connect to the controller).

Next, generate two secrets with length between 16 and 255 characters; one to use as an API key, and another to use as the shared Relay secret. Use your favorite secret/password generator, or use slskd by running:

```bash
./slskd --generate-secret 32
```

For this example we'll use `9tWy5c3NrmekKVWLQXBztz0hY7rNGlj1tGMfvHKmU1q` for the API key, and `BgI04SuVtsAYipxPHDpdxJsnVoPEeq4tKJeorWxr3Pj` for the shared secret.

> Note: DO NOT copy/paste these values into your configuration! Use your own values, or you're opening yourself up to an attack!

### Controller

Next, we'll configure the controller.  We'll enable the Relay, set this instance's mode to `controller`, and configure a single agent named `example_agent`.  We'll accept connections from within our home network, which uses IP addresses in the 192.168.1.x range.

We'll also need to create an API key, which we'll allow to be used from anywhere.  We'll give the key the role `readwrite`, which is required for agents.

```yaml
instance_name: controller
relay:
  enabled: true
  mode: controller
  agents:
    example_agent:
      secret: BgI04SuVtsAYipxPHDpdxJsnVoPEeq4tKJeorWxr3Pj
      cidr: 192.168.1.0/24
web:
  authentication:
    api_keys:
      example_api_key:
        key: 9tWy5c3NrmekKVWLQXBztz0hY7rNGlj1tGMfvHKmU1q
        role: readwrite
        cidr: 192.168.1.0/24
```

We'll start the agent, which is running on a machine with IP address 192.168.1.25.  We should see the following logs (among many others):

```bash
<todo: paste here>
```

### Agent

Finally, we'll configure the agent.  We'll enable the Relay, set this instance's mode to `agent`, and fill in the details of or controller.

The controller is running on a machine with IP address 192.168.1.53, and we want to use HTTPS, so we'll use the URL `https://192.168.1.53:5001`.  We're using the default self-signed certificate, so we'll need to `ignore_certificate_errors`.

We'll copy the API key and secret from the controller config, and lastly, we want the agent to receive the files downloaded by the controller, so we'll set `downloads` to `true`.

```yaml
relay:
  enabled: true
  mode: agent
  controller:
    address: https://192.168.1.53:5001
    ignore_certificate_errors: true
    api_key: 9tWy5c3NrmekKVWLQXBztz0hY7rNGlj1tGMfvHKmU1q
    secret: BgI04SuVtsAYipxPHDpdxJsnVoPEeq4tKJeorWxr3Pj
    downloads: true
```

We'll start the agent and confirm that it connects to the controller and uploads its shares:

```bash
<todo: paste here>
```
