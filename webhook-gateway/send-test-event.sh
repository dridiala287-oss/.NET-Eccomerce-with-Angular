#!/bin/bash
# usage: ./send-test-event.sh <url> [api_version] [payment_intent_id]
URL=${1:-http://127.0.0.1:9000/api/payments/webhook}
APIV=${2:-2025-05-28.basil}
PI=${3:-pi_test}
WH=$(grep '^STRIPE_WEBHOOK_SECRET=' ~/ecommerce/deploy/.env | cut -d= -f2)
TS=$(date +%s)
BODY=$(printf '{"id":"evt_test","object":"event","api_version":"%s","created":%s,"livemode":false,"pending_webhooks":1,"request":{"id":null,"idempotency_key":null},"type":"payment_intent.succeeded","data":{"object":{"id":"%s","object":"payment_intent","amount":1000,"currency":"usd","status":"succeeded"}}}' "$APIV" "$TS" "$PI")
SIG=$(printf '%s.%s' "$TS" "$BODY" | openssl dgst -sha256 -hmac "$WH" | awk '{print $2}')
curl -s -w "\nHTTP %{http_code}\n" -X POST "$URL" -H "Content-Type: application/json" -H "Stripe-Signature: t=$TS,v1=$SIG" -d "$BODY"
