import 'package:flutter/material.dart';
import 'package:miclink/app/theme/app_colors.dart';

class MuteButton extends StatelessWidget {
  const MuteButton({
    super.key,
    required this.icon,
    required this.activeIcon,
    required this.label,
    required this.isMuted,
    required this.onPressed,
  });

  final IconData icon;
  final IconData activeIcon;
  final String label;
  final bool isMuted;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final active = !isMuted;

    return Column(
      children: [
        Material(
          color: Colors.transparent,
          child: InkWell(
            onTap: onPressed,
            borderRadius: BorderRadius.circular(28),
            child: AnimatedContainer(
              duration: const Duration(milliseconds: 200),
              width: 120,
              height: 120,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: active
                    ? AppColors.accentGradient
                    : const LinearGradient(
                        colors: [AppColors.surfaceLight, AppColors.surface],
                      ),
                border: Border.all(
                  color: active
                      ? AppColors.accentGlow.withValues(alpha: 0.5)
                      : AppColors.surfaceLight,
                  width: 2,
                ),
                boxShadow: active
                    ? [
                        BoxShadow(
                          color: AppColors.accent.withValues(alpha: 0.35),
                          blurRadius: 28,
                          spreadRadius: 2,
                        ),
                      ]
                    : null,
              ),
              child: Icon(
                active ? activeIcon : icon,
                size: 44,
                color: active ? Colors.white : AppColors.textMuted,
              ),
            ),
          ),
        ),
        const SizedBox(height: 14),
        Text(
          label,
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: active ? AppColors.textPrimary : AppColors.textMuted,
              ),
        ),
        const SizedBox(height: 4),
        Text(
          active ? 'Вкл' : 'Выкл',
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: active ? AppColors.success : AppColors.danger,
                fontWeight: FontWeight.w600,
              ),
        ),
      ],
    );
  }
}
