import 'package:flutter/material.dart';
import 'package:miclink/app/theme/app_colors.dart';
import 'package:miclink/domain/enums/connection_status.dart';
import 'package:miclink/domain/models/device_connection_info.dart';

class ConnectionStatusCard extends StatelessWidget {
  const ConnectionStatusCard({super.key, required this.info});

  final DeviceConnectionInfo info;

  @override
  Widget build(BuildContext context) {
    final statusColor = _statusColor(info.status);

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: AppColors.surface.withValues(alpha: 0.85),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.surfaceLight),
        boxShadow: [
          BoxShadow(
            color: statusColor.withValues(alpha: 0.15),
            blurRadius: 24,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'ПОДКЛЮЧЕНИЕ',
            style: Theme.of(context).textTheme.labelLarge,
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Container(
                width: 10,
                height: 10,
                decoration: BoxDecoration(
                  color: statusColor,
                  shape: BoxShape.circle,
                  boxShadow: [
                    BoxShadow(
                      color: statusColor.withValues(alpha: 0.6),
                      blurRadius: 8,
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  info.status.label,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          _InfoRow(label: 'ПК', value: info.pcName ?? 'Не найден'),
          const SizedBox(height: 8),
          _InfoRow(label: 'Транспорт', value: info.transport),
        ],
      ),
    );
  }

  Color _statusColor(ConnectionStatus status) => switch (status) {
        ConnectionStatus.connected => AppColors.success,
        ConnectionStatus.discovering ||
        ConnectionStatus.pairing ||
        ConnectionStatus.connecting ||
        ConnectionStatus.reconnecting =>
          AppColors.warning,
        ConnectionStatus.error => AppColors.danger,
        ConnectionStatus.disconnected => AppColors.textMuted,
      };
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: Theme.of(context).textTheme.bodyMedium),
        Text(
          value,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w500,
              ),
        ),
      ],
    );
  }
}
