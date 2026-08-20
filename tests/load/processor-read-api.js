import http from 'k6/http';
import { check } from 'k6';

const defaultProcessorBaseUrl = 'http://localhost:5154';
const defaultDeviceId = 'DRILL-001';
const defaultDuration = '30s';
const defaultDeviceRequestsPerSecond = 10;
const defaultReadingRequestsPerSecond = 40;
const defaultReadingLimit = 100;

const processorBaseUrl =
  __ENV.PROCESSOR_BASE_URL ?? defaultProcessorBaseUrl;

const deviceId = __ENV.DEVICE_ID ?? defaultDeviceId;
const duration = __ENV.DURATION ?? defaultDuration;

const deviceRequestsPerSecond = readPositiveInteger(
  'DEVICE_REQUESTS_PER_SECOND',
  defaultDeviceRequestsPerSecond,
);

const readingRequestsPerSecond = readPositiveInteger(
  'READING_REQUESTS_PER_SECOND',
  defaultReadingRequestsPerSecond,
);

const readingLimit = readPositiveInteger(
  'READING_LIMIT',
  defaultReadingLimit,
);

/**
 * Configures a small, repeatable baseline rather than a stress test.
 */
export const options = {
  scenarios: {
    list_devices: {
      executor: 'constant-arrival-rate',
      exec: 'readDevices',
      rate: deviceRequestsPerSecond,
      timeUnit: '1s',
      duration,
      preAllocatedVUs: 5,
      maxVUs: 20,
    },
    read_history: {
      executor: 'constant-arrival-rate',
      exec: 'readHistory',
      rate: readingRequestsPerSecond,
      timeUnit: '1s',
      duration,
      preAllocatedVUs: 20,
      maxVUs: 100,
    },
  },
  thresholds: {
    checks: ['rate>0.99'],
    dropped_iterations: ['count==0'],
    http_req_failed: ['rate<0.01'],
    'http_req_duration{endpoint:devices}': [
      'p(95)<200',
      'p(99)<500',
    ],
    'http_req_duration{endpoint:readings}': [
      'p(95)<250',
      'p(99)<500',
    ],
  },
};

/**
 * Requests the identifiers that have persisted telemetry.
 */
export function readDevices() {
  const response = http.get(
    `${processorBaseUrl}/api/telemetry/devices`,
    {
      headers: { Accept: 'application/json' },
      tags: { endpoint: 'devices' },
    },
  );

  check(response, {
    'devices returns HTTP 200': current => current.status === 200,
    'devices returns a JSON array': current => isJsonArray(current),
  });
}

/**
 * Requests the recent persisted readings for one device.
 */
export function readHistory() {
  const encodedDeviceId = encodeURIComponent(deviceId);

  const response = http.get(
    `${processorBaseUrl}/api/telemetry/readings/${encodedDeviceId}` +
      `?limit=${readingLimit}`,
    {
      headers: { Accept: 'application/json' },
      tags: { endpoint: 'readings' },
    },
  );

  check(response, {
    'readings returns HTTP 200': current => current.status === 200,
    'readings returns a JSON array': current => isJsonArray(current),
  });
}

/**
 * Determines whether an HTTP response contains a JSON array.
 *
 * @param {import('k6/http').RefinedResponse<'text'>} response
 * HTTP response to inspect.
 * @returns {boolean} True when the response body is a JSON array.
 */
function isJsonArray(response) {
  try {
    return Array.isArray(response.json());
  } catch {
    return false;
  }
}

/**
 * Reads a positive integer from the k6 environment.
 *
 * @param {string} variableName Environment variable name.
 * @param {number} defaultValue Value used when the variable is absent.
 * @returns {number} Parsed positive integer.
 */
function readPositiveInteger(variableName, defaultValue) {
  const rawValue = __ENV[variableName];

  if (rawValue === undefined || rawValue === '') {
    return defaultValue;
  }

  const parsedValue = Number(rawValue);

  if (!Number.isInteger(parsedValue) || parsedValue <= 0) {
    throw new Error(
      `${variableName} must be a positive integer.`,
    );
  }

  return parsedValue;
}
