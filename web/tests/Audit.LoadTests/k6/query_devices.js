import http from "k6/http";
import { check } from "k6";

export const options = {
  vus: 10,
  duration: "1m",
};

export default function () {
  const res = http.get("https://audit.example.local/api/v1/admin/devices", {
    headers: { Authorization: `Bearer ${__ENV.ACCESS_TOKEN || ""}` },
  });

  check(res, {
    "status is 200 or 401": (r) => r.status === 200 || r.status === 401,
  });
}
