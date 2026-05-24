#!/usr/bin/env bash
# Creates Kafka topics. Called automatically by kafka-init container in docker-compose.
# Run manually: docker exec kafka kafka-topics.sh --bootstrap-server localhost:9092 --list

set -e

BOOTSTRAP="kafka:9092"

create_topic() {
  local name=$1
  local partitions=${2:-3}
  kafka-topics.sh \
    --bootstrap-server "$BOOTSTRAP" \
    --create \
    --if-not-exists \
    --topic "$name" \
    --partitions "$partitions" \
    --replication-factor 1
  echo "Topic '$name' ready."
}

create_topic "order-created"    3
create_topic "payment-processed" 3

echo "All topics created successfully."
