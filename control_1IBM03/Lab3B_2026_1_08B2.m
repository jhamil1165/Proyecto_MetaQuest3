% Laboratorio 3 - Parte B - 08B2 (ciclo 2026-1)
% Lugar geometrico de las raices y linealizacion - 1IBM03
% Correr como script .m (no .mlx) para que los DataTips del rlocus funcionen bien
clc; clear; close all;

% a) Linealizacion de dC/dt = -ke*C + Ki*u/(1 + al*C) en C0 = 1, u0 = 1
syms C u ke Ki al real
f = -ke*C + Ki*u/(1 + al*C);
C0 = 1; u0 = 1;

dfdC = diff(f, C);
dfdu = diff(f, u);
a1 = simplify(subs(dfdC, [C u], [C0 u0]));   % df/dC en el punto
b1 = simplify(subs(dfdu, [C u], [C0 u0]));   % df/du en el punto
f0 = simplify(subs(f, [C u], [C0 u0]));      % f en el punto

f_lin = f0 + a1*(C - C0) + b1*(u - u0);      % Taylor de primer orden
disp('a) df/dC evaluada:'); disp(a1)
disp('a) df/du evaluada:'); disp(b1)
disp('a) f(C0,u0):'); disp(f0)
disp('a) dC/dt linealizada ='); disp(f_lin)
% Ojo: f(C0,u0) no es cero, el punto (1,1) no es de equilibrio,
% por eso se conserva el termino constante f0 en la ecuacion linealizada

% b) Reemplazo de parametros ke = 0.5, Ki = 2, al = 1
par = [0.5 2 1];
f_nl = subs(f, [ke Ki al], par);
f_l = expand(subs(f_lin, [ke Ki al], par));
disp('b) No lineal: dC/dt ='); disp(f_nl)
disp('b) Lineal:    dC/dt ='); disp(f_l)       % 1/2 - C + u  (equivale a 0.5 - (C-1) + (u-1))

a_num = double(subs(a1, [ke Ki al], par));    % -1
b_num = double(subs(b1, [ke Ki al], par));    %  1
f0_num = double(subs(f0, [ke Ki al], par));   %  0.5

% FT en variables de desviacion: dC' = a_num*C' + b_num*u'
s = tf('s');
G_lin = b_num/(s - a_num);
disp('b) FT linealizada deltaC(s)/deltaU(s):'); G_lin   % 1/(s+1), tau = 1 s
% tau = 1/|a_num| = 1 s -> se estabiliza en aprox. 4*tau = 4 s

% c) Simulink: comparacion lineal vs no lineal
% Observaciones del enunciado:
% - La frase "pulso unitario con magnitud" esta cortada. Asumo pulso de amplitud 1.
% - El codigo de la pista (y = -1 + (-b*(x1 - b^2))) + b*x2) es solo un ejemplo:
%   tiene un parentesis ")" de mas y no corresponde a este sistema.
%
% Bloques (Stop time = 40 s):
% 1) Pulse Generator (Sources): Pulse type = Time based, Amplitude = 1,
%    Period = 20 s, Pulse width = 50 %, Phase delay = 0  -> u(t)
% 2) MATLAB Function "No lineal" (User-Defined Functions) con 2 ENTRADAS (C, u):
%       function dC = fcn(C, u)
%       ke = 0.5; Ki = 2; al = 1;
%       dC = -ke*C + Ki*u/(1 + al*C);
%       end
% 3) Integrator: Initial condition = 1 (C0). Entrada = dC, salida = C.
%    Realimentar la salida C del integrador a la entrada "C" del MATLAB Function.
%    La salida del Pulse Generator va a la entrada "u".
% 4) MATLAB Function "Lineal" con 2 ENTRADAS (C, u):
%       function dC = fcn(C, u)
%       dC = 0.5 - 1*(C - 1) + 1*(u - 1);
%       end
% 5) Otro Integrator con Initial condition = 1, conectado igual que en 3)
%    (el mismo Pulse Generator entra a la entrada "u").
% 6) Mux (2 entradas): C no lineal y C lineal -> Scope.
%    En el Scope: Legend activada, eje x en s.
%
% Verificacion equivalente en MATLAB (ode45) para contrastar con el Scope
upulso = @(t) double(mod(t, 20) < 10);
f_nlin = @(t, C) -0.5*C + 2*upulso(t)./(1 + C);
f_lin2 = @(t, C) f0_num + a_num*(C - C0) + b_num*(upulso(t) - u0);
op = odeset('MaxStep', 0.01);
[t1, C1] = ode45(f_nlin, [0 40], C0, op);
[t2, C2] = ode45(f_lin2, [0 40], C0, op);

figure;
subplot(2,1,1)
plot(t1, upulso(t1), 'k', 'LineWidth', 1.2); grid on
xlabel('Tiempo (s)'); ylabel('u(t) (u.a.)'); title('Entrada: pulso de amplitud 1')
ylim([-0.2 1.2])
subplot(2,1,2)
plot(t1, C1, 'b', t2, C2, 'r--', 'LineWidth', 1.2); grid on
xlabel('Tiempo (s)'); ylabel('C(t) (u.a.)')
legend('No lineal', 'Linealizada'); title('Concentracion plasmatica')
% Con u = 1 el no lineal tiende a C = 1.56 y el lineal a 1.5 (cerca del punto).
% Con u = 0 el no lineal cae a 0 y el lineal solo baja a 0.5: al alejarse de
% u0 = 1 la aproximacion de Taylor deja de ser valida

% d) Lazo cerrado en terminos de K. C(s) = 1.5K, G(s), S(s) = 6
% (el enunciado escribe 1,5k en minuscula, se toma k = K)
syms sy K real
Gs = 2*(sy + 0.2)/((sy + 7)*(3*sy^2 + 2.1*sy + 0.9));
Cs = 1.5*K;
Ss = 6;
T = simplify(Cs*Gs/(1 + Cs*Gs*Ss));
[nT, dT] = numden(T);
disp('d) T(s) = Y(s)/X(s) ='); pretty(simplify(nT/dT))
% T(s) = 3K(s + 0.2) / (3s^3 + 23.1s^2 + (15.6 + 18K)s + (6.3 + 3.6K))

G = 2*(s + 0.2)/((s + 7)*(3*s^2 + 2.1*s + 0.9));
S = 6;
L = 1.5*G*S;          % lazo abierto sin K (K es la ganancia del rlocus)

% e) LGR y puntos de ruptura
figure;
rlocus(L); grid on
title('LGR - sistema de bombas de infusion')
xlabel('Eje real (s^{-1})'); ylabel('Eje imaginario (s^{-1})')

den = (sy + 7)*(3*sy^2 + 2.1*sy + 0.9);
ec = expand(den + 1.5*K*2*(sy + 0.2)*6);      % ecuacion caracteristica = 0
disp('e) Ecuacion caracteristica:'); disp(vpa(ec, 5))
K_s = solve(ec, K);                            % K despejado en funcion de s
dK = simplify(diff(K_s, sy));
[nd, ~] = numden(dK);
rp = double(solve(nd == 0, sy));               % candidatos
Kp = double(subs(K_s, sy, rp));
ok = abs(imag(rp)) < 1e-6 & real(Kp) > 0;      % solo reales con K > 0
rp_val = real(rp(ok))
Kp_val = real(Kp(ok))
% rp = -3.694 (K = 1.791) salida del eje real
% rp = -0.670 (K = 0.628) llegada (ingreso) al eje real
% rp = 0.214 se descarta (K < 0)
hold on
plot(rp_val, zeros(size(rp_val)), 'ks', 'MarkerSize', 9, 'MarkerFaceColor', 'g')
hold off

% f) Interseccion con el eje imaginario y Routh-Hurwitz
% En el LGR con DataTips se ve que ninguna rama cruza el eje jw para K > 0:
% polos (-7, -0.35 +/- j0.42), cero (-0.2), asintotas verticales en -3.75.
% Routh con 3s^3 + 23.1s^2 + (15.6 + 18K)s + (6.3 + 3.6K):
%  s^3 | 3                 15.6 + 18K
%  s^2 | 23.1              6.3 + 3.6K
%  s^1 | b1                0
%  s^0 | 6.3 + 3.6K
a3 = 3; a2 = 23.1; a1r = 15.6 + 18*K; a0 = 6.3 + 3.6*K;
b1r = simplify((a2*a1r - a3*a0)/a2);
disp('f) Fila s^1:'); disp(vpa(b1r, 5))
K_b1 = double(solve(b1r == 0, K))              % -0.843
K_a0 = double(solve(a0 == 0, K))               % -1.75
% Estable si K > -0.843. Para cualquier K > 0 el sistema es estable,
% entonces no hay K positivo que lo lleve al limite de estabilidad.
% El unico K critico es K = -0.843 (no fisico), con polos en:
w_lim = sqrt(double(subs(a0, K, K_b1))/a2)     % s = +/- j0.376 rad/s

% g) Comportamiento cuando K aumenta
p_ol = pole(L)
z_ol = zero(L)
sigma_a = (sum(p_ol) - sum(z_ol))/(length(p_ol) - length(z_ol))   % -3.75
ang_a = (2*(0:1) + 1)*180/(length(p_ol) - length(z_ol))           % 90 y 270
% - 0 < K < 0.628: par complejo dominante subamortiguado que se va a la izquierda
% - K = 0.628: el par llega al eje real en s = -0.67
% - 0.628 < K < 1.791: tres polos reales (sobreamortiguado). Uno va al cero -0.2
%   y el otro se junta con el polo que viene de -7
% - K = 1.791: salen del eje real en s = -3.694
% - K > 1.791: par complejo que sube por la asintota vertical sigma = -3.75,
%   wn crece y zeta baja (mas oscilacion). El polo real se acerca a -0.2
% - Nunca cruza a semiplano derecho: es estable para todo K > 0
% - Ganancia DC = 0.6K/(6.3 + 3.6K) -> tiende a 1/6 cuando K crece

% h) Aproximacion de segundo orden, escalon de 0.4
% Tomo K = 20: el polo real (-0.211) casi se cancela con el cero (-0.2)
% y el par complejo (-3.74 +/- j10.47) es el que define la respuesta
% (para K < 0.628 el cero -0.2 esta muy cerca del par y la respuesta no se
% parece a la de segundo orden)
K_el = 20;
T_cl = feedback(1.5*K_el*G, S);
p_cl = pole(T_cl)
pc = p_cl(imag(p_cl) > 0);
wn = abs(pc)
zeta = -real(pc)/wn
k_dc = dcgain(T_cl);
T2 = k_dc*wn^2/(s^2 + 2*zeta*wn*s + wn^2);    % segundo orden equivalente

t = 0:0.001:15;
x = step(0.4*T_cl, t);
x2 = step(0.4*T2, t);
figure;
plot(t, x, 'b', t, x2, 'r--', 'LineWidth', 1.2); grid on
xlabel('Tiempo (s)'); ylabel('x(t) (u.a.)')
legend('Sistema completo', 'Aprox. 2do orden')
title('Respuesta a escalon de 0.4, K = 20')
info_real = stepinfo(0.4*T_cl)
info_aprox = stepinfo(0.4*T2)
% Referencia calculada: Mp real aprox. 40 %, Mp aprox. 32.5 %, x_ss = 0.061
% Se cumple de forma aproximada: el pico y la oscilacion coinciden, pero el
% polo -0.211 no se cancela del todo con el cero -0.2 y deja una cola lenta
% (tau aprox. 1/0.211 = 4.7 s) que alarga el tiempo de establecimiento

% i) Sensibilidad normalizada con A(s) = K(1 + Td*s)
% Asumo que el amplificador reemplaza a K dentro de C(s): C(s) = 1.5K(1 + Td*s)
syms Td real
Ci = 1.5*K*(1 + Td*sy);
GT = Ci*Gs/(1 + Ci*Gs*Ss);
SK = simplify(diff(GT, K)*K/GT);
disp('i) Sensibilidad S_K^GT ='); pretty(SK)
% S_K^GT = (s+7)(3s^2+2.1s+0.9) / [(s+7)(3s^2+2.1s+0.9) + 18K(1+Td*s)(s+0.2)]
%        = 1/(1 + C(s)G(s)S(s))
% Si se toma C(s) = K(1 + Td*s) sin el 1.5, el 18K cambia a 12K
% A mayor K, S_K baja: el lazo es menos sensible a cambios de ganancia
