import 'package:flutter/material.dart';

class AppColors {
  AppColors._();

  static const Color backgroundTop = Color(0xFF0F0F1A);
  static const Color backgroundBottom = Color(0xFF1A1A2E);
  static const Color surface = Color(0xFF1E1E2E);
  static const Color surfaceLight = Color(0xFF2A2A3D);
  static const Color accent = Color(0xFF6C5CE7);
  static const Color accentGlow = Color(0xFF8B7CF6);
  static const Color success = Color(0xFF00B894);
  static const Color warning = Color(0xFFFDCB6E);
  static const Color danger = Color(0xFFE17055);
  static const Color textPrimary = Color(0xFFF5F5FA);
  static const Color textMuted = Color(0xFFA0A0B8);

  static const LinearGradient background = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [backgroundTop, backgroundBottom],
  );

  static const LinearGradient accentGradient = LinearGradient(
    colors: [accent, accentGlow],
  );
}
