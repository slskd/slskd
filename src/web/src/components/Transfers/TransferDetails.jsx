import { formatBytes, formatDate } from '../../lib/util';
import { Table } from 'semantic-ui-react';

// Regex patterns for field name formatting
const UPPERCASE_PATTERN = /([A-Z])/gu;
const FIRST_CHAR_PATTERN = /^./u;
const DURATION_PATTERN = /^\d{2}:\d{2}:\d{2}/u;

// Convert camelCase to Title Case with spaces
const formatFieldName = (fieldName) => {
  return fieldName
    .replaceAll(UPPERCASE_PATTERN, ' $1')
    .replace(FIRST_CHAR_PATTERN, (string) => string.toUpperCase())
    .trim();
};

// Format value based on field type
const formatValue = (key, value) => {
  if (value === null || value === undefined) {
    return 'N/A';
  }

  const lowerKey = key.toLowerCase();

  // Format datetime fields
  if (
    (lowerKey.includes('at') || lowerKey.includes('time')) &&
    typeof value === 'string' &&
    value.includes(':')
  ) {
    // Check if it's a duration (HH:MM:SS format)
    if (DURATION_PATTERN.test(value)) {
      return value;
    }

    // Otherwise it's a datetime
    return formatDate(value);
  }

  // Format byte-related fields
  if (lowerKey.includes('bytes') || key === 'size') {
    return formatBytes(value);
  }

  // Format speed
  if (lowerKey.includes('speed')) {
    return `${formatBytes(value)}/s`;
  }

  // Format percentage
  if (lowerKey.includes('percent')) {
    if (typeof value === 'number') {
      return `${value.toFixed(2)}%`;
    }

    return String(value);
  }

  return String(value);
};

const TransferDetails = ({ file }) => {
  // Fields to display in the popup
  const fields = [
    'id',
    'username',
    'direction',
    'filename',
    'size',
    'startOffset',
    'state',
    'requestedAt',
    'enqueuedAt',
    'startedAt',
    'endedAt',
    'bytesTransferred',
    'averageSpeed',
    'bytesRemaining',
    'elapsedTime',
    'percentComplete',
    'remainingTime',
  ];

  return (
    <Table
      basic="very"
      compact
      size="small"
    >
      <Table.Body>
        {fields.map((field) => {
          const value = file[field];
          return (
            <Table.Row key={field}>
              <Table.Cell style={{ fontWeight: 'bold', paddingRight: '1em' }}>
                {formatFieldName(field)}
              </Table.Cell>
              <Table.Cell>{formatValue(field, value)}</Table.Cell>
            </Table.Row>
          );
        })}
      </Table.Body>
    </Table>
  );
};

export default TransferDetails;
