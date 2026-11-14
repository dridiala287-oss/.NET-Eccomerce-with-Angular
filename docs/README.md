# Hybrid cloud e-commerce deployment notes

Local stack on noeud1 (Docker): nginx, API + Angular, SQL Server, Redis, Prometheus, Grafana, Jenkins.
Cloud: Stripe webhook gateway on Render, reached through a Cloudflare quick tunnel.
