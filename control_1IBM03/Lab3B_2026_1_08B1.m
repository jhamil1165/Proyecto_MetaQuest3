% Laboratorio 3 - Parte B - 08B1 (ciclo 2026-1)
% Lugar geometrico de las raices y linealizacion - 1IBM03
% Correr como script .m (no .mlx) para que los DataTips del rlocus funcionen bien
clc; clear; close all;

% ===== Pregunta 1 =====
s = tf('s');
G = 8*(s + 4)/((2*s)*(0.2*s + 1)*(s^2 + 10*s + 30));
S = 1;

% a) Lazo cerrado en terminos de k
syms sy k real
Gs = 8*(sy + 4)/((2*sy)*(0.2*sy + 1)*(sy^2 + 10*sy + 30));
T = simplify(k*Gs/(1 + k*Gs*S));
[nT, dT] = numden(T);
disp('a) T(s) = Y(s)/X(s) ='); pretty(simplify(nT/dT))
% T(s) = 20k(s + 4) / (s^4 + 15s^3 + 80s^2 + (150 + 20k)s + 80k)

% b) LGR, angulo e interseccion de asintotas
figure;
rlocus(G*S); grid on
title('LGR - Pregunta 1')
xlabel('Eje real (s^{-1})'); ylabel('Eje imaginario (s^{-1})')

figure;
pzmap(G*S); grid on
title('Polos y ceros de lazo abierto')

p = pole(G*S)          % 0, -5, -5 +/- j2.236
z = zero(G*S)          % -4
n_a = length(p) - length(z);             % 3 asintotas
ang_a = (2*(0:n_a-1) + 1)*180/n_a        % 60, 180, 300 grados
Cg = (sum(p) - sum(z))/n_a               % -3.667

% c) Angulo de salida desde el polo complejo superior
pc = p(imag(p) > 0);
p_otros = p(abs(p - pc) > 1e-6);
th_z = angle(pc - z)*180/pi;             % aporte del cero
th_p = angle(pc - p_otros)*180/pi;       % aporte de los otros polos
th_sal = 180 + sum(th_z) - sum(th_p)     % -41.81 grados (= 318.19)
% Del polo -5 - j2.236 sale con +41.81 grados (simetrico)

% d) k para Mp = 15 %
Mp = 0.15;
zeta = -log(Mp)/sqrt(pi^2 + log(Mp)^2)   % 0.517
figure;
rlocus(G*S); grid on
sgrid(zeta, []);
title('LGR con linea de \zeta = 0.517')
xlabel('Eje real (s^{-1})'); ylabel('Eje imaginario (s^{-1})')
% Con DataTips, poner el cursor donde la rama del par dominante corta la linea de zeta
k15 = 4.05;   % valor leido del datatip (referencia calculada: k = 4.05, polos -1.75 +/- j2.90)

% e) Respuesta con k15 a escalon unitario
T_cl = feedback(k15*G, S);
t = 0:0.001:6;
y = step(T_cl, t);
figure;
plot(t, y, 'b', 'LineWidth', 1.2); grid on
xlabel('Tiempo (s)'); ylabel('y(t) (u.a.)')
title(['Respuesta al escalon unitario, k = ' num2str(k15)])
info_e = stepinfo(T_cl)
% Verificar con DataTip en el pico que Mp <= 20 %
% Referencia calculada: Mp aprox. 11.8 %, tp aprox. 1.28 s, ts aprox. 1.85 s
% Sale menor a 15 % porque el polo real -3.55 cerca del cero -4 y el polo -7.9
% no son del todo despreciables (el sistema no es 2do orden puro)

% f) Limite de estabilidad
% Routh con s^4 + 15s^3 + 80s^2 + (150 + 20k)s + 80k
%  s^4 | 1        80         80k
%  s^3 | 15       150 + 20k
%  s^2 | b1       80k
%  s^1 | c1
%  s^0 | 80k
a4 = 1; a3 = 15; a2 = 80; a1 = 150 + 20*k; a0 = 80*k;
b1 = simplify((a3*a2 - a4*a1)/a3);       % 70 - 4k/3
c1 = simplify((b1*a1 - a3*a0)/b1);
disp('f) b1 ='); disp(b1)
disp('f) c1 ='); disp(c1)
k_lim = double(solve(c1 == 0, k));
k_lim = k_lim(k_lim > 0)                 % 19.84
w_lim = sqrt(double(subs(a0/b1, k, k_lim)))   % 6.04 rad/s -> s = +/- j6.04
% Estable para 0 < k < 19.84
% En el LGR, con DataTips en el cruce con el eje imaginario:
k1 = 19.84;   % valor leido del datatip (referencia Routh: 19.84, s = +/- j6.04)
k2 = 22;      % un poco mayor que k1 -> criticamente inestable

T1 = feedback(k1*G, S);
T2 = feedback(k2*G, S);
t = 0:0.001:15;
y1 = step(T1, t);
y2 = step(T2, t);
figure;
subplot(2,1,1)
plot(t, y1, 'b', 'LineWidth', 1.2); grid on
xlabel('Tiempo (s)'); ylabel('y(t) (u.a.)')
title(['k_1 = ' num2str(k1) ' (criticamente estable)'])
subplot(2,1,2)
plot(t, y2, 'r', 'LineWidth', 1.2); grid on
xlabel('Tiempo (s)'); ylabel('y(t) (u.a.)')
title(['k_2 = ' num2str(k2) ' (criticamente inestable)'])
% k1: oscilacion sostenida de periodo 2*pi/6.04 = 1.04 s
% k2: oscilacion que crece con el tiempo

% ===== Pregunta 2 =====
% a) Linealizacion de tau*dV/dt = -V + 1/(1 + exp(-a(V - Vth))) + I en V0 = 1, I0 = 4
syms V I tau a Vth real
f = (-V + 1/(1 + exp(-a*(V - Vth))) + I)/tau;   % dV/dt = f(V,I)
V0 = 1; I0 = 4;
dfdV = diff(f, V);
dfdI = diff(f, I);
aV = simplify(subs(dfdV, [V I], [V0 I0]));
bI = simplify(subs(dfdI, [V I], [V0 I0]));
f0 = simplify(subs(f, [V I], [V0 I0]));
f_lin = f0 + aV*(V - V0) + bI*(I - I0);
disp('P2 a) dV/dt linealizada ='); disp(f_lin)
% f(V0,I0) no es cero: el punto no es de equilibrio, se mantiene f0

% b) Reemplazo tau = 0.8, a = 2, Vth = 1
par = [0.8 2 1];
f_nl = subs(f, [tau a Vth], par);
f_l = expand(subs(f_lin, [tau a Vth], par));
disp('P2 b) No lineal: dV/dt ='); disp(f_nl)
disp('P2 b) Lineal:    dV/dt ='); disp(f_l)
aV_n = double(subs(aV, [tau a Vth], par))     % -0.625
bI_n = double(subs(bI, [tau a Vth], par))     %  1.25
f0_n = double(subs(f0, [tau a Vth], par))     %  4.375
G_lin = bI_n/(s - aV_n)                        % 1.25/(s + 0.625), tau_eq = 1.6 s

% c) Simulink: comparacion lineal vs no lineal
% Bloques (Stop time = 20 s):
% 1) Step (Sources): Step time = 1, Initial value = 0, Final value = 1 -> I(t)
% 2) MATLAB Function "No lineal" con 2 ENTRADAS (V, I):
%       function dV = fcn(V, I)
%       tau = 0.8; a = 2; Vth = 1;
%       dV = (-V + 1/(1 + exp(-a*(V - Vth))) + I)/tau;
%       end
% 3) Integrator: Initial condition = 1 (V0). Su salida V se realimenta a la
%    entrada "V" del MATLAB Function; el Step va a la entrada "I".
% 4) MATLAB Function "Lineal" con 2 ENTRADAS (V, I):
%       function dV = fcn(V, I)
%       dV = 4.375 - 0.625*(V - 1) + 1.25*(I - 4);
%       end
% 5) Otro Integrator con Initial condition = 1, conectado igual.
% 6) Mux (2 entradas) -> Scope, con leyenda.
%
% Verificacion equivalente en MATLAB (ode45) para contrastar con el Scope
Iin = @(t) double(t >= 1);
f_nlin = @(t, V) (-V + 1./(1 + exp(-2*(V - 1))) + Iin(t))/0.8;
f_lin2 = @(t, V) f0_n + aV_n*(V - V0) + bI_n*(Iin(t) - I0);
op = odeset('MaxStep', 0.01);
[t1, V1] = ode45(f_nlin, [0 20], V0, op);
[t2, V2] = ode45(f_lin2, [0 20], V0, op);
figure;
plot(t1, V1, 'b', t2, V2, 'r--', 'LineWidth', 1.2); grid on
xlabel('Tiempo (s)'); ylabel('V(t) (u.a.)')
legend('No lineal', 'Linealizada')
title('Potencial de membrana ante escalon unitario de corriente')
% El escalon lleva I a 1, lejos de I0 = 4, por eso las curvas se separan:
% la linealizacion solo vale cerca del punto de operacion
